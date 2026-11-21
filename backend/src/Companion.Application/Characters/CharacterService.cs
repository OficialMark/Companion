using Companion.Application.Campaigns;
using Companion.Application.Catalog;
using Companion.Application.Contracts;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Characters;

/// <summary>
/// The player's own sheet, inventory, spells and saved rolls (spec 011). The owner is always the
/// participant of the session making the call (FR-003); no method takes a participant id from the
/// client. Every write recalculates the character with <see cref="CharacterRules"/> before saving,
/// so the HP clamp and the combat profile sync land in the same commit, and events go out after it.
/// </summary>
public sealed class CharacterService(
    ICompanionDbContext db,
    ParticipantAuthorization authorization,
    ICampaignNotifier notifier,
    IPresenceTracker presence,
    TimeProvider timeProvider)
{
    public const string ChangedEvent = "CharacterChanged";
    private const string ItemNotFound = "Item não encontrado na sua ficha.";

    private sealed class Loaded(Participant participant, CharacterSheet sheet)
    {
        public Participant Participant { get; } = participant;
        public CharacterSheet Sheet { get; } = sheet;
        public List<InventoryItem> Items { get; init; } = [];
        public List<CharacterSpell> Spells { get; init; } = [];
        public List<SavedRoll> Rolls { get; init; } = [];
        public List<CharacterFeature> Features { get; init; } = [];
        public List<string> Notices { get; } = [];
    }

    public const string NoSheetYet = "Você ainda não tem ficha.";

    /// <summary>
    /// Returns the caller's sheet. With <paramref name="ifExists"/> a missing sheet is a 404 and is
    /// not created (the combat profile panel asks this way, so looking never creates a sheet).
    /// </summary>
    public async Task<CharacterDto> GetAsync(Guid campaignId, string token, CancellationToken cancellationToken, bool ifExists = false)
    {
        var loaded = await LoadAsync(campaignId, token, cancellationToken, createIfMissing: !ifExists);
        var stats = CharacterRules.Compute(loaded.Sheet.Data, Entries(loaded), SpellEntries(loaded), FeatureEntries(loaded));
        // The same name in another campaign is another participant with the same sheet: bring this
        // table's initiative and HP in line with the sheet the first time it is read there (FR-010).
        var participant = loaded.Participant;
        var expected = ((int?)Math.Clamp(stats.Initiative.Value, -100, 100), (int?)loaded.Sheet.Data.CurrentHitPoints, (int?)stats.MaxHitPoints.Value);
        if ((participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints) != expected)
        {
            participant.UpdateCombatProfile(expected.Item1, expected.Item2, expected.Item3);
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyAsync(campaignId, "ParticipantUpdated", DtoMapper.ToDto(participant, presence.IsOnline(participant.Id)), cancellationToken);
        }

        return ToDto(loaded, stats);
    }

    public Task<CharacterDto> SaveSheetAsync(Guid campaignId, string token, SaveSheetRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da ficha.");
            db.ExpectVersion(loaded.Sheet, ParseVersion(request.Version));
            Validate(() => loaded.Sheet.Update(data, timeProvider.GetUtcNow()));
            db.MarkModified(loaded.Sheet);
        });

    public Task<CharacterDto> AddItemAsync(Guid campaignId, string token, SaveItemRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados do item.");
            AddItem(loaded, data with { Equipped = false }, null);
        });

    public Task<CharacterDto> AddItemFromCatalogAsync(Guid campaignId, string token, AddFromCatalogRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            // What this campaign sees (spec 013): the master's version replaces the global entry here.
            var catalog = CatalogScope.ResolveItem(db, campaignId, request.CatalogItemId);
            var quantity = request.Quantity ?? 1;
            if (quantity is < 1 or > 9_999) throw new AppException(AppErrorKind.Validation, "A quantidade deve ficar entre 1 e 9999.");
            var template = WithBaseItem(campaignId, catalog, request.BaseCatalogItemId);
            template = template with
            {
                Quantity = quantity,
                Equipped = false,
                Proficient = DefaultProficiency(loaded.Sheet.Data.GeneralProficiencies, template)
            };
            AddItem(loaded, template, catalog.Id);
        });

    public Task<CharacterDto> SaveItemAsync(Guid campaignId, string token, Guid itemId, SaveItemRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var item = FindItem(loaded, itemId);
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados do item.");
            if (request.Version is not null) db.ExpectVersion(item, ParseVersion(request.Version));
            // Equipping goes through EquipAsync so the armor/shield limit is enforced in one place.
            Validate(() => item.Update(data with { Equipped = item.Data.Equipped && InventoryItemData.IsEquippable(data.Category) }));
            RepairEquipment(loaded);
        });

    public Task<CharacterDto> EquipAsync(Guid campaignId, string token, Guid itemId, EquipItemRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            FindItem(loaded, itemId);
            EquipResult result = null!;
            Validate(() => result = EquipmentRules.SetEquipped(Entries(loaded), itemId, request.Equipped));
            foreach (var change in result.Changes) loaded.Items.Single(x => x.Id == change.ItemId).SetEquipped(change.Equipped);
            loaded.Notices.AddRange(result.Notices);
        });

    public Task<CharacterDto> RemoveItemAsync(Guid campaignId, string token, Guid itemId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var item = FindItem(loaded, itemId);
            db.Remove(item);
            loaded.Items.Remove(item);
        });

    public Task<CharacterDto> AddSpellAsync(Guid campaignId, string token, SaveSpellRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da magia.");
            AddSpell(loaded, data, null);
        });

    public Task<CharacterDto> AddSpellFromCatalogAsync(Guid campaignId, string token, AddSpellFromCatalogRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var catalog = CatalogScope.ResolveSpell(db, campaignId, request.CatalogSpellId);
            // A global spell and every table's version of it are the same spell (spec 013): the sheet is global
            // per name, so a copy taken in another campaign, or before the master's edit, still counts. The sheet
            // stores the root (the global spell an edit replaces), which outlives the table's copy: deleting the
            // copy or the whole campaign never makes the same spell addable twice.
            var root = catalog.BaseId ?? catalog.Id;
            // Rows that stored a copy's id (none expected, but cheap to honour) are matched through the live copies.
            var lineage = db.CatalogSpells.Where(x => x.Id == root || x.BaseId == root).Select(x => x.Id).ToHashSet();
            lineage.Add(root);
            if (loaded.Spells.Any(x => x.CatalogSpellId is { } id && lineage.Contains(id)))
                throw new AppException(AppErrorKind.Conflict, $"{catalog.Name} já está na sua ficha (talvez em outra versão). Remova a antiga para adicionar esta.");
            AddSpell(loaded, catalog.Template, root);
        });

    public Task<CharacterDto> SaveSpellAsync(Guid campaignId, string token, Guid spellId, SaveSpellRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var spell = loaded.Spells.FirstOrDefault(x => x.Id == spellId)
                ?? throw new AppException(AppErrorKind.NotFound, "Magia não encontrada na sua ficha.");
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da magia.");
            Validate(() => spell.Update(data));
        });

    public Task<CharacterDto> RemoveSpellAsync(Guid campaignId, string token, Guid spellId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var spell = loaded.Spells.FirstOrDefault(x => x.Id == spellId)
                ?? throw new AppException(AppErrorKind.NotFound, "Magia não encontrada na sua ficha.");
            db.Remove(spell);
            loaded.Spells.Remove(spell);
        });

    public Task<CharacterDto> AddRollAsync(Guid campaignId, string token, SaveRollRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            if (loaded.Rolls.Count >= SavedRoll.MaxRollsPerCharacter)
                throw new AppException(AppErrorKind.Validation, $"Você pode salvar até {SavedRoll.MaxRollsPerCharacter} rolagens.");
            var data = CheckRollReferences(loaded, request.Data);
            SavedRoll roll = null!;
            Validate(() => roll = new SavedRoll(Guid.NewGuid(), loaded.Sheet.ParticipantId, NextOrder(loaded.Rolls.Select(x => x.SortOrder)), data, timeProvider.GetUtcNow()));
            db.Add(roll);
            loaded.Rolls.Add(roll);
        });

    public Task<CharacterDto> SaveRollAsync(Guid campaignId, string token, Guid rollId, SaveRollRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var roll = FindRoll(loaded, rollId);
            var data = CheckRollReferences(loaded, request.Data);
            Validate(() => roll.Update(data));
        });

    public Task<CharacterDto> DuplicateRollAsync(Guid campaignId, string token, Guid rollId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var source = FindRoll(loaded, rollId);
            if (loaded.Rolls.Count >= SavedRoll.MaxRollsPerCharacter)
                throw new AppException(AppErrorKind.Validation, $"Você pode salvar até {SavedRoll.MaxRollsPerCharacter} rolagens.");
            var name = $"{source.Data.Name} (cópia)";
            if (name.Length > SavedRollData.MaxNameLength) name = name[..SavedRollData.MaxNameLength].TrimEnd();
            var copy = new SavedRoll(Guid.NewGuid(), loaded.Sheet.ParticipantId, NextOrder(loaded.Rolls.Select(x => x.SortOrder)),
                source.Data with { Name = name }, timeProvider.GetUtcNow());
            db.Add(copy);
            loaded.Rolls.Add(copy);
        });

    public Task<CharacterDto> RemoveRollAsync(Guid campaignId, string token, Guid rollId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var roll = FindRoll(loaded, rollId);
            db.Remove(roll);
            loaded.Rolls.Remove(roll);
        });

    // ---- Features with a use counter (spec 013, US2). The server owns the counter: deltas and rests are
    // applied to the stored value, so two quick taps are two spends, never one overwritten by the other.

    public const string FeatureNotFound = "Característica não encontrada na sua ficha.";

    public Task<CharacterDto> AddFeatureAsync(Guid campaignId, string token, SaveFeatureRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            if (loaded.Features.Count >= CharacterFeature.MaxFeaturesPerCharacter)
                throw new AppException(AppErrorKind.Validation, $"A ficha aceita até {CharacterFeature.MaxFeaturesPerCharacter} características.");
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da característica.");
            CharacterFeature feature = null!;
            Validate(() => feature = new CharacterFeature(Guid.NewGuid(), loaded.Sheet.ParticipantId,
                NextOrder(loaded.Features.Select(x => x.SortOrder)), data, timeProvider.GetUtcNow()));
            db.Add(feature);
            loaded.Features.Add(feature);
        });

    public Task<CharacterDto> SaveFeatureAsync(Guid campaignId, string token, Guid featureId, SaveFeatureRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var feature = FindFeature(loaded, featureId);
            var data = request.Data ?? throw new AppException(AppErrorKind.Validation, "Envie os dados da característica.");
            Validate(() => feature.Update(data));
        });

    public Task<CharacterDto> RemoveFeatureAsync(Guid campaignId, string token, Guid featureId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var feature = FindFeature(loaded, featureId);
            db.Remove(feature);
            loaded.Features.Remove(feature);
        });

    public Task<CharacterDto> SpendFeatureAsync(Guid campaignId, string token, Guid featureId, FeatureUsesRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            var feature = FindFeature(loaded, featureId);
            if (feature.Data.MaxUses is null) throw new AppException(AppErrorKind.Validation, $"{feature.Data.Name} não tem contador de usos.");
            if (request.Delta is < -CharacterFeatureData.MaxUsesLimit or > CharacterFeatureData.MaxUsesLimit or 0)
                throw new AppException(AppErrorKind.Validation, $"Use um valor entre -{CharacterFeatureData.MaxUsesLimit} e {CharacterFeatureData.MaxUsesLimit}, diferente de zero.");
            feature.Spend(request.Delta);
        });

    public Task<CharacterDto> RestoreFeatureAsync(Guid campaignId, string token, Guid featureId, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded => FindFeature(loaded, featureId).Restore());

    /// <summary>Swaps the feature with its neighbour above (-1) or below (+1); at an end it stays put.</summary>
    public Task<CharacterDto> MoveFeatureAsync(Guid campaignId, string token, Guid featureId, MoveFeatureRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            if (request.Direction is not (-1 or 1)) throw new AppException(AppErrorKind.Validation, "Mova para cima (-1) ou para baixo (1).");
            var feature = FindFeature(loaded, featureId);
            var ordered = loaded.Features.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
            var index = ordered.IndexOf(feature);
            var target = index + request.Direction;
            if (target < 0 || target >= ordered.Count) return;
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
            // Renumber densely: legacy or concurrent rows with the same order still end in a stable sequence.
            for (var i = 0; i < ordered.Count; i++) ordered[i].MoveTo(i);
            loaded.Features.Clear();
            loaded.Features.AddRange(ordered);
        });

    /// <summary>A short rest restores short-rest features; a long rest restores those and the long-rest ones (FR-014).</summary>
    public Task<CharacterDto> RestAsync(Guid campaignId, string token, RestRequest request, CancellationToken cancellationToken) =>
        WriteAsync(campaignId, token, cancellationToken, loaded =>
        {
            if (request.Kind is not { } kind || !Enum.IsDefined(kind))
                throw new AppException(AppErrorKind.Validation, "Escolha descanso curto ou longo.");
            var restored = loaded.Features.Count(feature => feature.Rest(kind));
            var label = kind == RestKind.Long ? "Descanso longo" : "Descanso curto";
            loaded.Notices.Add(restored switch
            {
                0 => $"{label}: nada para recuperar.",
                1 => $"{label}: 1 característica recuperada.",
                _ => $"{label}: {restored} características recuperadas."
            });
        });

    private static CharacterFeature FindFeature(Loaded loaded, Guid featureId) =>
        loaded.Features.FirstOrDefault(x => x.Id == featureId) ?? throw new AppException(AppErrorKind.NotFound, FeatureNotFound);

    private async Task<CharacterDto> WriteAsync(Guid campaignId, string token, CancellationToken cancellationToken, Action<Loaded> change)
    {
        var loaded = await LoadAsync(campaignId, token, cancellationToken);
        var participant = loaded.Participant;
        var before = (participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints);

        change(loaded);

        var stats = CharacterRules.Compute(loaded.Sheet.Data, Entries(loaded), SpellEntries(loaded), FeatureEntries(loaded));
        loaded.Sheet.ClampCurrentHitPoints(stats.MaxHitPoints.Value, timeProvider.GetUtcNow());
        // Every write updates the sheet row with the version read at load (or the client's version, set
        // by SaveSheetAsync) as the expected original. Two writes of the same owner that read the same
        // version cannot both commit: the second gets DbUpdateConcurrencyException (409) and its whole
        // SaveChanges rolls back. This is what keeps the 200 item limit and the one armor rule under
        // parallel requests, since those checks run on the lists loaded in memory.
        db.MarkModified(loaded.Sheet);

        // FR-010: the table (initiative order, HP bars) uses the same numbers as the sheet.
        var after = (stats.Initiative.Value, loaded.Sheet.Data.CurrentHitPoints, stats.MaxHitPoints.Value);
        var profileChanged = before != ((int?)after.Item1, (int?)after.Item2, (int?)after.Item3);
        if (profileChanged)
            participant.UpdateCombatProfile(Math.Clamp(after.Item1, -100, 100), after.Item2, after.Item3);

        // The same name in other campaigns is the same sheet: their tables get the new numbers too, so a
        // master there never sees stale HP nor gets blocked editing a profile the sheet now owns.
        var expected = ((int?)Math.Clamp(after.Item1, -100, 100), (int?)after.Item2, (int?)after.Item3);
        var ownerKey = loaded.Sheet.OwnerKey;
        var others = db.Participants
            .Where(x => x.Role == ParticipantRole.Player && x.NameKey == ownerKey && x.Id != participant.Id)
            .ToList();
        var othersChanged = others
            .Where(x => (x.InitiativeModifier, x.CurrentHitPoints, x.MaxHitPoints) != expected)
            .ToList();
        foreach (var other in othersChanged)
            other.UpdateCombatProfile(expected.Item1, expected.Item2, expected.Item3);

        await db.SaveChangesAsync(cancellationToken);

        if (profileChanged)
            await notifier.NotifyAsync(campaignId, "ParticipantUpdated", DtoMapper.ToDto(participant, presence.IsOnline(participant.Id)), cancellationToken);
        await notifier.NotifyAsync(campaignId, ChangedEvent, new { participantId = participant.Id }, cancellationToken);
        foreach (var other in othersChanged)
            await notifier.NotifyAsync(other.CampaignId, "ParticipantUpdated", DtoMapper.ToDto(other, presence.IsOnline(other.Id)), cancellationToken);
        foreach (var other in others)
            await notifier.NotifyAsync(other.CampaignId, ChangedEvent, new { participantId = other.Id }, cancellationToken);
        return ToDto(loaded, stats);
    }

    private async Task<Loaded> LoadAsync(Guid campaignId, string token, CancellationToken cancellationToken, bool createIfMissing = true)
    {
        var participant = authorization.Require(campaignId, token);
        if (participant.Role != ParticipantRole.Player)
            throw new AppException(AppErrorKind.Forbidden, "Somente jogadores possuem ficha pessoal.");

        // One sheet per player name, in every campaign (2026-11-20): look it up by the account key.
        var ownerKey = CharacterOwner.Key(participant);
        var sheet = db.CharacterSheets.FirstOrDefault(x => x.OwnerKey == ownerKey);
        if (sheet is null && db.CharacterSheets.FirstOrDefault(x => x.ParticipantId == participant.Id) is { } own)
        {
            // This participant created a sheet whose key no longer matches its name key (the migration's
            // SQL UPPER/TRIM differs from PersonName.Key for exotic names, or a legacy duplicate lost the
            // backfill). No sheet holds the current key, so adopting the row is safe; creating a new one
            // would collide with its primary key forever.
            own.AssignOwner(ownerKey, timeProvider.GetUtcNow());
            db.MarkModified(own);
            await db.SaveChangesAsync(cancellationToken);
            sheet = own;
        }

        if (sheet is null && !createIfMissing) throw new AppException(AppErrorKind.NotFound, NoSheetYet);
        if (sheet is null)
        {
            var now = timeProvider.GetUtcNow();
            sheet = new CharacterSheet(participant.Id, ownerKey, now);
            // A new sheet starts from the HP the player already gave the table, and from then on the sheet
            // is the only source of initiative and HP (FR-010): sync the participant in the same commit,
            // so the table never shows the old manual numbers next to a sheet that says otherwise.
            if (participant.MaxHitPoints is >= 1 and <= 9_999)
            {
                var max = participant.MaxHitPoints.Value;
                var current = Math.Clamp(participant.CurrentHitPoints ?? max, 0, max);
                sheet.Update(sheet.Data with { BaseMaxHitPoints = max, CurrentHitPoints = current }, now);
            }

            var stats = CharacterRules.Compute(sheet.Data, [], []);
            var before = (participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints);
            participant.UpdateCombatProfile(Math.Clamp(stats.Initiative.Value, -100, 100), sheet.Data.CurrentHitPoints, stats.MaxHitPoints.Value);
            var profileChanged = before != (participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints);
            db.Add(sheet);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (profileChanged)
                    await notifier.NotifyAsync(campaignId, "ParticipantUpdated", DtoMapper.ToDto(participant, presence.IsOnline(participant.Id)), cancellationToken);
            }
            catch (Exception exception) when (exception.GetType().Name is "DbUpdateException")
            {
                // Two first opens at the same time: the other request created it. Use that one.
                db.Detach(sheet);
                var existing = db.CharacterSheets.FirstOrDefault(x => x.OwnerKey == ownerKey);
                if (existing is null) throw;
                sheet = existing;
            }
        }

        return new Loaded(participant, sheet)
        {
            Items = db.InventoryItems.Where(x => x.ParticipantId == sheet.ParticipantId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList(),
            Spells = db.CharacterSpells.Where(x => x.ParticipantId == sheet.ParticipantId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList(),
            Rolls = db.SavedRolls.Where(x => x.ParticipantId == sheet.ParticipantId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList(),
            Features = db.CharacterFeatures.Where(x => x.ParticipantId == sheet.ParticipantId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList()
        };
    }

    private void AddItem(Loaded loaded, InventoryItemData data, Guid? catalogItemId)
    {
        if (loaded.Items.Count >= InventoryItem.MaxItemsPerCharacter)
            throw new AppException(AppErrorKind.Validation, $"A mochila aceita até {InventoryItem.MaxItemsPerCharacter} itens.");
        InventoryItem item = null!;
        Validate(() => item = new InventoryItem(Guid.NewGuid(), loaded.Sheet.ParticipantId,
            NextOrder(loaded.Items.Select(x => x.SortOrder)), data, catalogItemId, timeProvider.GetUtcNow()));
        db.Add(item);
        loaded.Items.Add(item);
    }

    private void AddSpell(Loaded loaded, CharacterSpellData data, Guid? catalogSpellId)
    {
        if (loaded.Spells.Count >= CharacterSpell.MaxSpellsPerCharacter)
            throw new AppException(AppErrorKind.Validation, $"A ficha aceita até {CharacterSpell.MaxSpellsPerCharacter} magias.");
        CharacterSpell spell = null!;
        Validate(() => spell = new CharacterSpell(Guid.NewGuid(), loaded.Sheet.ParticipantId,
            NextOrder(loaded.Spells.Select(x => x.SortOrder)), data, catalogSpellId, timeProvider.GetUtcNow()));
        db.Add(spell);
        loaded.Spells.Add(spell);
    }

    /// <summary>Saved roll references must point at this owner's items and spells (never someone else's ids).</summary>
    private static SavedRollData CheckRollReferences(Loaded loaded, SavedRollData? data)
    {
        if (data is null) throw new AppException(AppErrorKind.Validation, "Envie a configuração da rolagem.");
        if (data.InventoryItemId is { } itemId && loaded.Items.All(x => x.Id != itemId))
            throw new AppException(AppErrorKind.Validation, ItemNotFound);
        if (data.CharacterSpellId is { } spellId && loaded.Spells.All(x => x.Id != spellId))
            throw new AppException(AppErrorKind.Validation, "Magia não encontrada na sua ficha.");
        return data;
    }

    /// <summary>
    /// A generic magic item ("Arma +1") gets its weapon or armor block from a mundane catalog item of
    /// the same category; the name says both ("Arma +1 (Espada Longa)").
    /// </summary>
    private InventoryItemData WithBaseItem(Guid campaignId, Domain.Catalog.CatalogItem catalog, Guid? baseId)
    {
        if (!catalog.RequiresBaseItem) return catalog.Template;
        var kind = catalog.Category == ItemCategory.Weapon ? "a arma" : "a armadura";
        if (baseId is null) throw new AppException(AppErrorKind.Validation, $"Escolha {kind} base de {catalog.Name}.");
        Domain.Catalog.CatalogItem? baseItem;
        try { baseItem = CatalogScope.ResolveItem(db, campaignId, baseId.Value); }
        catch (AppException) { baseItem = null; }
        if (baseItem is null || baseItem.RequiresBaseItem || baseItem.Category != catalog.Category ||
            baseItem.Rarity is not null || baseItem.Template.MagicBonus != 0)
            throw new AppException(AppErrorKind.Validation, $"Escolha {kind} comum que servirá de base.");
        var name = $"{catalog.Name} ({baseItem.Name})";
        if (name.Length > InventoryItemData.MaxNameLength) name = name[..InventoryItemData.MaxNameLength].TrimEnd();
        return catalog.Template with
        {
            Name = name,
            Weapon = baseItem.Template.Weapon,
            Armor = baseItem.Template.Armor,
            Weight = catalog.Template.Weight ?? baseItem.Template.Weight
        };
    }

    /// <summary>FR-030: the general proficiencies are only the starting value of an item's own flag.</summary>
    public static bool DefaultProficiency(GeneralProficiencies general, InventoryItemData item) => item.Category switch
    {
        ItemCategory.Weapon => item.Weapon?.Class == WeaponClass.Martial ? general.MartialWeapons : general.SimpleWeapons,
        ItemCategory.Armor => item.Armor is not null && general.Armor.Contains(item.Armor.Type),
        ItemCategory.Shield => general.Shields,
        _ => false
    };

    private void RepairEquipment(Loaded loaded)
    {
        foreach (var change in EquipmentRules.Repair(Entries(loaded)))
            loaded.Items.Single(x => x.Id == change.ItemId).SetEquipped(change.Equipped);
    }

    private static InventoryItem FindItem(Loaded loaded, Guid itemId) =>
        loaded.Items.FirstOrDefault(x => x.Id == itemId) ?? throw new AppException(AppErrorKind.NotFound, ItemNotFound);

    private static SavedRoll FindRoll(Loaded loaded, Guid rollId) =>
        loaded.Rolls.FirstOrDefault(x => x.Id == rollId) ?? throw new AppException(AppErrorKind.NotFound, "Rolagem não encontrada na sua ficha.");

    private static int NextOrder(IEnumerable<int> orders) => orders.DefaultIfEmpty(-1).Max() + 1;

    private static IReadOnlyList<ItemEntry> Entries(Loaded loaded) => loaded.Items.Select(x => x.ToEntry()).ToArray();

    private static IReadOnlyList<SpellEntry> SpellEntries(Loaded loaded) => loaded.Spells.Select(x => x.ToEntry()).ToArray();

    private static IReadOnlyList<FeatureEntry> FeatureEntries(Loaded loaded) => loaded.Features.Select(x => x.ToEntry()).ToArray();

    private static byte[] ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new AppException(AppErrorKind.Validation, "Envie a versão da ficha que você editou.");
        try { return Convert.FromBase64String(version); }
        catch (FormatException) { throw new AppException(AppErrorKind.Validation, "Versão da ficha inválida."); }
    }

    private static void Validate(Action action)
    {
        try { action(); }
        catch (ArgumentException exception) { throw new AppException(AppErrorKind.Validation, exception.Message); }
    }

    private static CharacterDto ToDto(Loaded loaded, CharacterStats stats)
    {
        var weaponIds = loaded.Items.Where(x => x.Data.Category == ItemCategory.Weapon).Select(x => x.Id).ToHashSet();
        var spellIds = loaded.Spells.Select(x => x.Id).ToHashSet();
        return new CharacterDto(
            loaded.Participant.Id,
            loaded.Sheet.Data,
            Convert.ToBase64String(loaded.Sheet.Version),
            loaded.Items.Select(x => new InventoryItemDto(x.Id, x.SortOrder, x.CatalogItemId, Convert.ToBase64String(x.Version), x.Data)).ToArray(),
            loaded.Spells.Select(x => new CharacterSpellDto(x.Id, x.SortOrder, x.CatalogSpellId, x.Data)).ToArray(),
            loaded.Rolls.Select(x => new SavedRollDto(x.Id, x.SortOrder,
                (x.Data.InventoryItemId is { } item && !weaponIds.Contains(item)) ||
                (x.Data.CharacterSpellId is { } spell && !spellIds.Contains(spell)), x.Data)).ToArray(),
            stats,
            loaded.Notices.ToArray(),
            loaded.Features.Select(x => new CharacterFeatureDto(x.Id, x.SortOrder,
                x.Data with { Effects = x.Data.Effects ?? [] })).ToArray());
    }
}
