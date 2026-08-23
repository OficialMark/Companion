using Companion.Application.Contracts;
using Companion.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Companion.Infrastructure.Persistence;

public sealed class CompanionDbContext(DbContextOptions<CompanionDbContext> options)
    : DbContext(options), ICompanionDbContext
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<Combatant> Combatants => Set<Combatant>();

    IQueryable<Campaign> ICompanionDbContext.Campaigns => Campaigns;
    IQueryable<Participant> ICompanionDbContext.Participants => Participants;
    IQueryable<DiceRoll> ICompanionDbContext.DiceRolls => DiceRolls;
    IQueryable<Encounter> ICompanionDbContext.Encounters => Encounters;
    IQueryable<Combatant> ICompanionDbContext.Combatants => Combatants;

    void ICompanionDbContext.Add(Campaign campaign) => Campaigns.Add(campaign);
    void ICompanionDbContext.Add(Participant participant) => Participants.Add(participant);
    void ICompanionDbContext.Add(DiceRoll diceRoll) => DiceRolls.Add(diceRoll);
    void ICompanionDbContext.Add(Encounter encounter) => Encounters.Add(encounter);
    void ICompanionDbContext.Add(Combatant combatant) => Combatants.Add(combatant);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CompanionDbContext).Assembly);
    }
}
