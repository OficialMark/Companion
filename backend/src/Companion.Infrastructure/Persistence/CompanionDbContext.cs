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

    IQueryable<Campaign> ICompanionDbContext.Campaigns => Campaigns;
    IQueryable<CampaignAccessCode> ICompanionDbContext.CampaignAccessCodes => CampaignAccessCodes;
    IQueryable<Participant> ICompanionDbContext.Participants => Participants;
    IQueryable<DiceRoll> ICompanionDbContext.DiceRolls => DiceRolls;
    IQueryable<Encounter> ICompanionDbContext.Encounters => Encounters;
    IQueryable<Combatant> ICompanionDbContext.Combatants => Combatants;

    void ICompanionDbContext.Add(Campaign campaign) => Campaigns.Add(campaign);
    void ICompanionDbContext.Add(CampaignAccessCode accessCode) => CampaignAccessCodes.Add(accessCode);
    void ICompanionDbContext.Add(Participant participant) => Participants.Add(participant);
    void ICompanionDbContext.Add(DiceRoll diceRoll) => DiceRolls.Add(diceRoll);
    void ICompanionDbContext.Add(Encounter encounter) => Encounters.Add(encounter);
    void ICompanionDbContext.Add(Combatant combatant) => Combatants.Add(combatant);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CompanionDbContext).Assembly);
    }

    public async Task<bool> DeleteCampaignAggregateAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
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
