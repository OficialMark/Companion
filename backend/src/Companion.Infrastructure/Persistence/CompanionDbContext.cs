using Companion.Application.Contracts;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Companion.Infrastructure.Persistence;

public sealed class CompanionDbContext(DbContextOptions<CompanionDbContext> options)
    : DbContext(options), ICompanionDbContext
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignAccessCode> CampaignAccessCodes => Set<CampaignAccessCode>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantAvatar> ParticipantAvatars => Set<ParticipantAvatar>();
    public DbSet<ParticipantSession> ParticipantSessions => Set<ParticipantSession>();
    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Combatant> Combatants => Set<Combatant>();
    public DbSet<CombatAttack> CombatAttacks => Set<CombatAttack>();
    public DbSet<CombatAttackTarget> CombatAttackTargets => Set<CombatAttackTarget>();
    public DbSet<Battlemap> Battlemaps => Set<Battlemap>();
    public DbSet<BattlemapTokenPosition> BattlemapTokenPositions => Set<BattlemapTokenPosition>();
    public DbSet<AttackArea> AttackAreas => Set<AttackArea>();
    public DbSet<BattlemapTokenAppearance> TokenAppearances => Set<BattlemapTokenAppearance>();
    public DbSet<BattlemapMarker> BattlemapMarkers => Set<BattlemapMarker>();
    public DbSet<Creature> Creatures => Set<Creature>();
    public DbSet<CreatureImage> CreatureImages => Set<CreatureImage>();
    public DbSet<SeedVersion> SeedVersions => Set<SeedVersion>();
    public DbSet<CharacterSheet> CharacterSheets => Set<CharacterSheet>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<CharacterSpell> CharacterSpells => Set<CharacterSpell>();
    public DbSet<SavedRoll> SavedRolls => Set<SavedRoll>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogSpell> CatalogSpells => Set<CatalogSpell>();
    public DbSet<CharacterFeature> CharacterFeatures => Set<CharacterFeature>();

    IQueryable<Campaign> ICompanionDbContext.Campaigns => Campaigns;
    IQueryable<CampaignAccessCode> ICompanionDbContext.CampaignAccessCodes => CampaignAccessCodes;
    IQueryable<Participant> ICompanionDbContext.Participants => Participants;
    IQueryable<ParticipantAvatar> ICompanionDbContext.ParticipantAvatars => ParticipantAvatars;
    IQueryable<ParticipantSession> ICompanionDbContext.ParticipantSessions => ParticipantSessions;
    IQueryable<DiceRoll> ICompanionDbContext.DiceRolls => DiceRolls;
    IQueryable<Encounter> ICompanionDbContext.Encounters => Encounters;
    IQueryable<Combatant> ICompanionDbContext.Combatants => Combatants;
    IQueryable<CombatAttack> ICompanionDbContext.CombatAttacks => CombatAttacks;
    IQueryable<CombatAttackTarget> ICompanionDbContext.CombatAttackTargets => CombatAttackTargets;
    IQueryable<Battlemap> ICompanionDbContext.Battlemaps => Battlemaps;
    IQueryable<BattlemapTokenPosition> ICompanionDbContext.BattlemapTokenPositions => BattlemapTokenPositions;
    IQueryable<AttackArea> ICompanionDbContext.AttackAreas => AttackAreas;
    IQueryable<BattlemapTokenAppearance> ICompanionDbContext.TokenAppearances => TokenAppearances;
    IQueryable<BattlemapMarker> ICompanionDbContext.BattlemapMarkers => BattlemapMarkers;
    IQueryable<Creature> ICompanionDbContext.Creatures => Creatures;
    IQueryable<CreatureImage> ICompanionDbContext.CreatureImages => CreatureImages;
    IQueryable<SeedVersion> ICompanionDbContext.SeedVersions => SeedVersions;
    IQueryable<CharacterSheet> ICompanionDbContext.CharacterSheets => CharacterSheets;
    IQueryable<InventoryItem> ICompanionDbContext.InventoryItems => InventoryItems;
    IQueryable<CharacterSpell> ICompanionDbContext.CharacterSpells => CharacterSpells;
    IQueryable<SavedRoll> ICompanionDbContext.SavedRolls => SavedRolls;
    IQueryable<CatalogItem> ICompanionDbContext.CatalogItems => CatalogItems;
    IQueryable<CatalogSpell> ICompanionDbContext.CatalogSpells => CatalogSpells;
    IQueryable<CharacterFeature> ICompanionDbContext.CharacterFeatures => CharacterFeatures;

    void ICompanionDbContext.Add(Campaign campaign) => Campaigns.Add(campaign);
    void ICompanionDbContext.Add(CampaignAccessCode accessCode) => CampaignAccessCodes.Add(accessCode);
    void ICompanionDbContext.Add(Participant participant) => Participants.Add(participant);
    void ICompanionDbContext.Add(ParticipantSession session) => ParticipantSessions.Add(session);
    void ICompanionDbContext.Remove(ParticipantSession session) => ParticipantSessions.Remove(session);
    void ICompanionDbContext.Detach(Participant participant) => Entry(participant).State = EntityState.Detached;
    void ICompanionDbContext.Detach(ParticipantSession session) => Entry(session).State = EntityState.Detached;

    void ICompanionDbContext.TouchSession(Guid sessionId, DateTimeOffset seenAt)
    {
        var staleBefore = seenAt - ParticipantSession.LastSeenResolution;
        ParticipantSessions.Where(x => x.Id == sessionId && x.LastSeenAt <= staleBefore)
            .ExecuteUpdate(setters => setters.SetProperty(x => x.LastSeenAt, seenAt));
    }

    void ICompanionDbContext.Add(ParticipantAvatar avatar) => ParticipantAvatars.Add(avatar);
    void ICompanionDbContext.Remove(ParticipantAvatar avatar) => ParticipantAvatars.Remove(avatar);
    void ICompanionDbContext.Add(DiceRoll diceRoll) => DiceRolls.Add(diceRoll);
    void ICompanionDbContext.Add(Encounter encounter) => Encounters.Add(encounter);
    void ICompanionDbContext.MarkModified(Encounter encounter) => Entry(encounter).State = EntityState.Modified;
    void ICompanionDbContext.Add(Combatant combatant) => Combatants.Add(combatant);
    void ICompanionDbContext.Add(CombatAttack attack) => CombatAttacks.Add(attack);
    void ICompanionDbContext.Add(CombatAttackTarget target) => CombatAttackTargets.Add(target);
    void ICompanionDbContext.Add(Battlemap battlemap) => Battlemaps.Add(battlemap);
    void ICompanionDbContext.Add(BattlemapTokenPosition position) => BattlemapTokenPositions.Add(position);
    void ICompanionDbContext.Remove(BattlemapTokenPosition position) => BattlemapTokenPositions.Remove(position);
    void ICompanionDbContext.Add(AttackArea area) => AttackAreas.Add(area);
    void ICompanionDbContext.Remove(AttackArea area) => AttackAreas.Remove(area);
    void ICompanionDbContext.Add(BattlemapTokenAppearance appearance) => TokenAppearances.Add(appearance);
    void ICompanionDbContext.Remove(BattlemapTokenAppearance appearance) => TokenAppearances.Remove(appearance);
    void ICompanionDbContext.Add(BattlemapMarker marker) => BattlemapMarkers.Add(marker);
    void ICompanionDbContext.Remove(BattlemapMarker marker) => BattlemapMarkers.Remove(marker);
    void ICompanionDbContext.Add(Creature creature) => Creatures.Add(creature);
    void ICompanionDbContext.Remove(Creature creature) => Creatures.Remove(creature);
    void ICompanionDbContext.Add(CreatureImage image) => CreatureImages.Add(image);
    void ICompanionDbContext.Remove(CreatureImage image) => CreatureImages.Remove(image);
    void ICompanionDbContext.Add(SeedVersion seedVersion) => SeedVersions.Add(seedVersion);
    void ICompanionDbContext.Add(CharacterSheet sheet) => CharacterSheets.Add(sheet);
    void ICompanionDbContext.Add(InventoryItem item) => InventoryItems.Add(item);
    void ICompanionDbContext.Remove(InventoryItem item) => InventoryItems.Remove(item);
    void ICompanionDbContext.Add(CharacterSpell spell) => CharacterSpells.Add(spell);
    void ICompanionDbContext.Remove(CharacterSpell spell) => CharacterSpells.Remove(spell);
    void ICompanionDbContext.Add(SavedRoll roll) => SavedRolls.Add(roll);
    void ICompanionDbContext.Remove(SavedRoll roll) => SavedRolls.Remove(roll);
    void ICompanionDbContext.Add(CatalogItem item) => CatalogItems.Add(item);
    void ICompanionDbContext.Add(CatalogSpell spell) => CatalogSpells.Add(spell);
    void ICompanionDbContext.Remove(CatalogItem item) => CatalogItems.Remove(item);
    void ICompanionDbContext.Remove(CatalogSpell spell) => CatalogSpells.Remove(spell);
    void ICompanionDbContext.Add(CharacterFeature feature) => CharacterFeatures.Add(feature);
    void ICompanionDbContext.Remove(CharacterFeature feature) => CharacterFeatures.Remove(feature);
    void ICompanionDbContext.Detach(CharacterSheet sheet) => Entry(sheet).State = EntityState.Detached;
    void ICompanionDbContext.ExpectVersion(CharacterSheet sheet, byte[] version) => Entry(sheet).Property(x => x.Version).OriginalValue = version;
    void ICompanionDbContext.ExpectVersion(InventoryItem item, byte[] version) => Entry(item).Property(x => x.Version).OriginalValue = version;
    void ICompanionDbContext.MarkModified(CharacterSheet sheet) => Entry(sheet).Property(x => x.UpdatedAt).IsModified = true;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CompanionDbContext).Assembly);
    }

    public async Task<bool> DeleteCampaignAggregateAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await CombatAttackTargets.Where(target => CombatAttacks
            .Where(attack => Encounters.Where(encounter => encounter.CampaignId == campaignId)
                .Select(encounter => encounter.Id).Contains(attack.EncounterId))
            .Select(attack => attack.Id).Contains(target.AttackId)).ExecuteDeleteAsync(cancellationToken);
        await CombatAttacks.Where(attack => Encounters.Where(encounter => encounter.CampaignId == campaignId)
            .Select(encounter => encounter.Id).Contains(attack.EncounterId)).ExecuteDeleteAsync(cancellationToken);
        await DiceRolls.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        await Combatants.Where(combatant => Encounters
            .Where(encounter => encounter.CampaignId == campaignId)
            .Select(encounter => encounter.Id)
            .Contains(combatant.EncounterId)).ExecuteDeleteAsync(cancellationToken);
        await Encounters.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        // Campaign creatures (own + edited copies); their images cascade in the database.
        await Creatures.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        // Campaign catalog entries (spec 013). Sheets keep their copies: they never point back here.
        await CatalogItems.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        await CatalogSpells.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        // Sessões antes dos participantes (a FK também cascateia, mas a limpeza fica explícita).
        // Character sheets are NOT deleted: they belong to the player name, across campaigns (2026-11-20).
        await ParticipantSessions.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        await Participants.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        await CampaignAccessCodes.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        var deleted = await Campaigns.Where(x => x.Id == campaignId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted > 0;
    }
}
