using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;

namespace Companion.Application.Contracts;

public interface ICompanionDbContext
{
    IQueryable<Campaign> Campaigns { get; }
    IQueryable<CampaignAccessCode> CampaignAccessCodes { get; }
    IQueryable<Participant> Participants { get; }
    IQueryable<ParticipantAvatar> ParticipantAvatars { get; }
    IQueryable<ParticipantSession> ParticipantSessions { get; }
    IQueryable<DiceRoll> DiceRolls { get; }
    IQueryable<Encounter> Encounters { get; }
    IQueryable<Combatant> Combatants { get; }
    IQueryable<CombatAttack> CombatAttacks { get; }
    IQueryable<CombatAttackTarget> CombatAttackTargets { get; }
    IQueryable<Battlemap> Battlemaps { get; }
    IQueryable<BattlemapTokenPosition> BattlemapTokenPositions { get; }
    IQueryable<AttackArea> AttackAreas { get; }
    IQueryable<BattlemapTokenAppearance> TokenAppearances { get; }
    IQueryable<BattlemapMarker> BattlemapMarkers { get; }
    IQueryable<Creature> Creatures { get; }
    IQueryable<CreatureImage> CreatureImages { get; }
    IQueryable<SeedVersion> SeedVersions { get; }
    IQueryable<CharacterSheet> CharacterSheets { get; }
    IQueryable<InventoryItem> InventoryItems { get; }
    IQueryable<CharacterSpell> CharacterSpells { get; }
    IQueryable<SavedRoll> SavedRolls { get; }
    IQueryable<CatalogItem> CatalogItems { get; }
    IQueryable<CatalogSpell> CatalogSpells { get; }

    void Add(Campaign campaign);
    void Add(CampaignAccessCode accessCode);
    void Add(Participant participant);
    void Add(ParticipantSession session);
    void Remove(ParticipantSession session);
    /// <summary>Descarta uma inserção pendente que falhou (corrida no join pelo nome), para o próximo SaveChanges não repeti-la.</summary>
    void Detach(Participant participant);
    void Detach(ParticipantSession session);
    /// <summary>
    /// Grava <see cref="ParticipantSession.LastSeenAt"/> direto no banco, só se o valor guardado tiver
    /// uma hora ou mais (a condição vai no WHERE, então duas requisições simultâneas gravam no máximo uma vez).
    /// </summary>
    void TouchSession(Guid sessionId, DateTimeOffset seenAt);
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
    void Add(BattlemapTokenAppearance appearance);
    void Remove(BattlemapTokenAppearance appearance);
    void Add(BattlemapMarker marker);
    void Remove(BattlemapMarker marker);
    void Add(Creature creature);
    void Remove(Creature creature);
    void Add(CreatureImage image);
    void Remove(CreatureImage image);
    void Add(SeedVersion seedVersion);
    void Add(CharacterSheet sheet);
    void Add(InventoryItem item);
    void Remove(InventoryItem item);
    void Add(CharacterSpell spell);
    void Remove(CharacterSpell spell);
    void Add(SavedRoll roll);
    void Remove(SavedRoll roll);
    void Add(CatalogItem item);
    void Add(CatalogSpell spell);
    /// <summary>Descarta uma ficha nova cuja inserção perdeu a corrida para outra requisição do mesmo dono.</summary>
    void Detach(CharacterSheet sheet);
    /// <summary>Usa a versão que o cliente leu na checagem de concorrência otimista (409 se outra gravação veio antes).</summary>
    void ExpectVersion(CharacterSheet sheet, byte[] version);
    void ExpectVersion(InventoryItem item, byte[] version);
    /// <summary>Força um UPDATE da linha da ficha para a rowversion mudar mesmo quando só os itens mudaram.</summary>
    void MarkModified(CharacterSheet sheet);
    /// <summary>Forces an UPDATE of the encounter row so its rowversion changes (optimistic concurrency).</summary>
    void MarkModified(Encounter encounter);
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