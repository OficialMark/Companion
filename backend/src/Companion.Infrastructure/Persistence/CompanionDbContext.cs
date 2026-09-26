using Companion.Application.Contracts;
using Companion.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Companion.Infrastructure.Persistence;

public sealed class CompanionDbContext(DbContextOptions<CompanionDbContext> options)
    : DbContext(options), ICompanionDbContext
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignAccessCode> CampaignAccessCodes => Set<CampaignAccessCode>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Combatant> Combatants => Set<Combatant>();
    public DbSet<CombatAttack> CombatAttacks => Set<CombatAttack>();
    public DbSet<CombatAttackTarget> CombatAttackTargets => Set<CombatAttackTarget>();
    public DbSet<Battlemap> Battlemaps => Set<Battlemap>();
    public DbSet<BattlemapTokenPosition> BattlemapTokenPositions => Set<BattlemapTokenPosition>();
    public DbSet<AttackArea> AttackAreas => Set<AttackArea>();
    public DbSet<BattlemapTokenAppearance> TokenAppearances => Set<BattlemapTokenAppearance>();

    IQueryable<Campaign> ICompanionDbContext.Campaigns => Campaigns;
    IQueryable<CampaignAccessCode> ICompanionDbContext.CampaignAccessCodes => CampaignAccessCodes;
    IQueryable<Participant> ICompanionDbContext.Participants => Participants;
    IQueryable<DiceRoll> ICompanionDbContext.DiceRolls => DiceRolls;
    IQueryable<Encounter> ICompanionDbContext.Encounters => Encounters;
    IQueryable<Combatant> ICompanionDbContext.Combatants => Combatants;
    IQueryable<CombatAttack> ICompanionDbContext.CombatAttacks => CombatAttacks;
    IQueryable<CombatAttackTarget> ICompanionDbContext.CombatAttackTargets => CombatAttackTargets;
    IQueryable<Battlemap> ICompanionDbContext.Battlemaps => Battlemaps;
    IQueryable<BattlemapTokenPosition> ICompanionDbContext.BattlemapTokenPositions => BattlemapTokenPositions;
    IQueryable<AttackArea> ICompanionDbContext.AttackAreas => AttackAreas;
    IQueryable<BattlemapTokenAppearance> ICompanionDbContext.TokenAppearances => TokenAppearances;

    void ICompanionDbContext.Add(Campaign campaign) => Campaigns.Add(campaign);
    void ICompanionDbContext.Add(CampaignAccessCode accessCode) => CampaignAccessCodes.Add(accessCode);
    void ICompanionDbContext.Add(Participant participant) => Participants.Add(participant);
    void ICompanionDbContext.Add(DiceRoll diceRoll) => DiceRolls.Add(diceRoll);
    void ICompanionDbContext.Add(Encounter encounter) => Encounters.Add(encounter);
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
        await Participants.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        await CampaignAccessCodes.Where(x => x.CampaignId == campaignId).ExecuteDeleteAsync(cancellationToken);
        var deleted = await Campaigns.Where(x => x.Id == campaignId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted > 0;
    }
}
