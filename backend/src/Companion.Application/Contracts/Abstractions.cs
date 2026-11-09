using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public interface ICompanionDbContext
{
    IQueryable<Campaign> Campaigns { get; }
    IQueryable<CampaignAccessCode> CampaignAccessCodes { get; }
    IQueryable<Participant> Participants { get; }
    IQueryable<ParticipantAvatar> ParticipantAvatars { get; }
    IQueryable<DiceRoll> DiceRolls { get; }
    IQueryable<Encounter> Encounters { get; }
    IQueryable<Combatant> Combatants { get; }
    IQueryable<CombatAttack> CombatAttacks { get; }
    IQueryable<CombatAttackTarget> CombatAttackTargets { get; }
    IQueryable<Battlemap> Battlemaps { get; }
    IQueryable<BattlemapTokenPosition> BattlemapTokenPositions { get; }
    IQueryable<AttackArea> AttackAreas { get; }

    void Add(Campaign campaign);
    void Add(CampaignAccessCode accessCode);
    void Add(Participant participant);
    void Add(ParticipantAvatar avatar);
    void Remove(ParticipantAvatar avatar);
    void Add(DiceRoll diceRoll);
    void Add(Encounter encounter);
    void Add(Combatant combatant);
    void Add(CombatAttack attack);
    void Add(CombatAttackTarget target);
    void Add(Battlemap battlemap);
    void Add(BattlemapTokenPosition position);
    void Remove(BattlemapTokenPosition position);
    void Add(AttackArea area);
    void Remove(AttackArea area);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteCampaignAggregateAsync(Guid campaignId, CancellationToken cancellationToken = default);
}

public sealed record ParticipantCredential(string Token, byte[] Hash);

public interface IParticipantTokenService
{
    ParticipantCredential Create();
    byte[] Hash(string token);
    string CreateCampaignCode();
}

public interface IDiceRandomizer
{
    int Roll(int faces);
}

public interface IPresenceTracker
{
    bool IsOnline(Guid participantId);
    bool Connected(Guid participantId, string connectionId);
    bool Disconnected(string connectionId, out Guid participantId);
}

public interface ICampaignNotifier
{
    Task NotifyAsync(Guid campaignId, string type, object payload, CancellationToken cancellationToken = default);
    Task ParticipantsChangedAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task EncounterInvalidatedAsync(Guid campaignId, Guid encounterId, CancellationToken cancellationToken = default);
    Task BattlemapInvalidatedAsync(Guid campaignId, Guid encounterId, string revision, CancellationToken cancellationToken = default);
}

public interface IAdminCredentialValidator
{
    bool IsValid(string credential);
}