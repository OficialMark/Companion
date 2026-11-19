using Companion.Application.Campaigns;
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
        public List<string> Notices { get; } = [];
    }

    public async Task<CharacterDto> GetAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(campaignId, token, cancellationToken);
        return ToDto(loaded, CharacterRules.Compute(loaded.Sheet.Data, Entries(loaded), SpellEntries(loaded)));
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
            var catalog = db.CatalogItems.FirstOrDefault(x => x.Id == request.CatalogItemId && !x.IsRemovedFromCatalog)
                ?? throw new AppException(AppErrorKind.NotFound, "Item não encontrado no catálogo.");
            var quantity = request.Quantity ?? 1;
            if (quantity is < 1 or > 9_999) throw new AppException(AppErrorKind.Validation, "A quantidade deve ficar entre 1 e 9999.");
            var template = WithBaseItem(catalog, request.BaseCatalogItemId);
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
            var catalog = db.CatalogSpells.FirstOrDefault(x => x.Id == request.CatalogSpellId && !x.IsRemovedFromCatalog)
                ?? throw new AppException(AppErrorKind.NotFound, "Magia não encontrada no catálogo.");
            if (loaded.Spells.Any(x => x.CatalogSpellId == catalog.Id))
                throw new AppException(AppErrorKind.Conflict, $"{catalog.Name} já está na sua ficha.");
            AddSpell(loaded, catalog.Template, catalog.Id);
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
            Validate(() => roll = new SavedRoll(Guid.NewGuid(), loaded.Participant.Id, NextOrder(loaded.Rolls.Select(x => x.SortOrder)), data, timeProvider.GetUtcNow()));
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
            var copy = new SavedRoll(Guid.NewGuid(), loaded.Participant.Id, NextOrder(loaded.Rolls.Select(x => x.SortOrder)),
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

    private async Task<CharacterDto> WriteAsync(Guid campaignId, string token, CancellationToken cancellationToken, Action<Loaded> change)
    {
        var loaded = await LoadAsync(campaignId, token, cancellationToken);
        var participant = loaded.Participant;
        var before = (participant.InitiativeModifier, participant.CurrentHitPoints, participant.MaxHitPoints);

        change(loaded);

        var stats = CharacterRules.Compute(loaded.Sheet.Data, Entries(loaded), SpellEntries(loaded));
        if (loaded.Sheet.ClampCurrentHitPoints(stats.MaxHitPoints.Value, timeProvider.GetUtcNow())) db.MarkModified(loaded.Sheet);

        // FR-010: the table (initiative order, HP bars) uses the same numbers as the sheet.
        var after = (stats.Initiative.Value, loaded.Sheet.Data.CurrentHitPoints, stats.MaxHitPoints.Value);
        var profileChanged = before != ((int?)after.Item1, (int?)after.Item2, (int?)after.Item3);
        if (profileChanged)
            participant.UpdateCombatProfile(Math.Clamp(after.Item1, -100, 100), after.Item2, after.Item3);

        await db.SaveChangesAsync(cancellationToken);

        if (profileChanged)
            await notifier.NotifyAsync(campaignId, "ParticipantUpdated", DtoMapper.ToDto(participant, presence.IsOnline(participant.Id)), cancellationToken);
        await notifier.NotifyAsync(campaignId, ChangedEvent, new { participantId = participant.Id }, cancellationToken);
        return ToDto(loaded, stats);
    }

    private async Task<Loaded> LoadAsync(Guid campaignId, string token, CancellationToken cancellationToken)
    {
        var participant = authorization.Require(campaignId, token);
        if (participant.Role != ParticipantRole.Player)
            throw new AppException(AppErrorKind.Forbidden, "Somente jogadores possuem ficha pessoal.");

        var sheet = db.CharacterSheets.FirstOrDefault(x => x.ParticipantId == participant.Id);
        if (sheet is null)
        {
            sheet = new CharacterSheet(participant.Id, timeProvider.GetUtcNow());
            db.Add(sheet);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception.GetType().Name is "DbUpdateException")
            {
                // Two first opens at the same time: the other request created it. Use that one.
                db.Detach(sheet);
                var existing = db.CharacterSheets.FirstOrDefault(x => x.ParticipantId == participant.Id);
                if (existing is null) throw;
                sheet = existing;
            }
        }

        return new Loaded(participant, sheet)
        {
            Items = db.InventoryItems.Where(x => x.ParticipantId == participant.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList(),
            Spells = db.CharacterSpells.Where(x => x.ParticipantId == participant.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList(),
            Rolls = db.SavedRolls.Where(x => x.ParticipantId == participant.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList()
        };
    }

    private void AddItem(Loaded loaded, InventoryItemData data, Guid? catalogItemId)
    {
        if (loaded.Items.Count >= InventoryItem.MaxItemsPerCharacter)
            throw new AppException(AppErrorKind.Validation, $"A mochila aceita até {InventoryItem.MaxItemsPerCharacter} itens.");
        InventoryItem item = null!;
        Validate(() => item = new InventoryItem(Guid.NewGuid(), loaded.Participant.Id,
            NextOrder(loaded.Items.Select(x => x.SortOrder)), data, catalogItemId, timeProvider.GetUtcNow()));
        db.Add(item);
        loaded.Items.Add(item);
    }

    private void AddSpell(Loaded loaded, CharacterSpellData data, Guid? catalogSpellId)
    {
        if (loaded.Spells.Count >= CharacterSpell.MaxSpellsPerCharacter)
            throw new AppException(AppErrorKind.Validation, $"A ficha aceita até {CharacterSpell.MaxSpellsPerCharacter} magias.");
        CharacterSpell spell = null!;
        Validate(() => spell = new CharacterSpell(Guid.NewGuid(), loaded.Participant.Id,
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
    private InventoryItemData WithBaseItem(Domain.Catalog.CatalogItem catalog, Guid? baseId)
    {
        if (!catalog.RequiresBaseItem) return catalog.Template;
        var kind = catalog.Category == ItemCategory.Weapon ? "a arma" : "a armadura";
        if (baseId is null) throw new AppException(AppErrorKind.Validation, $"Escolha {kind} base de {catalog.Name}.");
        var baseItem = db.CatalogItems.FirstOrDefault(x => x.Id == baseId && !x.IsRemovedFromCatalog);
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
            loaded.Notices.ToArray());
    }
}
