namespace Companion.Domain.Models;

public sealed class Encounter
{
    private readonly List<Combatant> _combatants = [];
    private Encounter() { }

    public Encounter(Guid id, Guid campaignId, DateTimeOffset createdAt)
    {
        Id = id;
        CampaignId = campaignId;
        CreatedAt = createdAt;
        Status = EncounterStatus.Preparing;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public EncounterStatus Status { get; private set; }
    public int Round { get; private set; }
    public int? CurrentTurnIndex { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public byte[] Version { get; private set; } = [];
    public IReadOnlyCollection<Combatant> Combatants => _combatants;

    public Combatant AddCombatant(
        Guid id,
        string name,
        int modifier,
        Guid? participantId = null,
        int? currentHitPoints = null,
        int? maxHitPoints = null)
    {
        EnsurePreparing();
        if (participantId.HasValue && _combatants.Any(x => x.ParticipantId == participantId))
        {
            throw new InvalidOperationException("Este participante já está na iniciativa.");
        }
        var nextCreationOrder = _combatants.Count == 0 ? 0 : _combatants.Max(x => x.CreationOrder) + 1;
        var combatant = new Combatant(id, Id, name, modifier, nextCreationOrder, participantId, currentHitPoints, maxHitPoints);
        _combatants.Add(combatant);
        return combatant;
    }

    public void UpdateCombatant(Guid combatantId, string name, int modifier, int? currentHitPoints = null, int? maxHitPoints = null)
    {
        var currentId = CurrentCombatantId();
        var combatant = FindCombatant(combatantId);
        combatant.Update(name, modifier, currentHitPoints, maxHitPoints);
        RestoreCurrentTurn(currentId);
    }

    public void UpdateHitPoints(Guid combatantId, int currentHitPoints) =>
        FindCombatant(combatantId).UpdateHitPoints(currentHitPoints);

    public IReadOnlyList<DamageApplication> ApplyDamage(IReadOnlyCollection<Guid> targetIds, int damage)
    {
        if (Status != EncounterStatus.Active)
        {
            throw new InvalidOperationException("O combate não está ativo.");
        }
        if (targetIds.Count == 0)
        {
            throw new InvalidOperationException("Selecione pelo menos um alvo.");
        }
        if (targetIds.Distinct().Count() != targetIds.Count)
        {
            throw new InvalidOperationException("Cada alvo deve aparecer somente uma vez.");
        }
        var targets = targetIds.Select(FindCombatant).ToArray();
        if (targets.Any(x => x.CurrentHitPoints is null))
        {
            throw new InvalidOperationException("Todos os alvos precisam ter vida informada.");
        }
        return targets.Select(x => x.ApplyDamage(damage)).ToArray();
    }

    public void RemoveCombatant(Guid combatantId)
    {
        var ordered = OrderedCombatants();
        var removedIndex = Array.FindIndex(ordered, x => x.Id == combatantId);
        if (removedIndex < 0) throw new InvalidOperationException("Combatente não encontrado.");
        var currentId = CurrentCombatantId();
        var removedCurrent = currentId == combatantId;
        Guid? successorId = null;
        if (removedCurrent && ordered.Length > 1) successorId = ordered[(removedIndex + 1) % ordered.Length].Id;
        _combatants.RemoveAll(x => x.Id == combatantId);
        RestoreCurrentTurn(removedCurrent ? successorId : currentId);
        if (Status == EncounterStatus.Active && _combatants.Count == 0) CurrentTurnIndex = null;
    }

    public void RollInitiative(Func<int> rollD20, Guid? combatantId = null)
    {
        if (Status == EncounterStatus.Ended) throw new InvalidOperationException("O combate já foi encerrado.");
        var currentCombatantId = CurrentCombatantId();
        var targets = combatantId.HasValue ? _combatants.Where(x => x.Id == combatantId.Value) : _combatants;
        var materialized = targets.ToArray();
        if (combatantId.HasValue && materialized.Length == 0) throw new InvalidOperationException("Combatente não encontrado.");
        foreach (var combatant in materialized) combatant.SetInitiative(rollD20());
        if (Status == EncounterStatus.Active && !combatantId.HasValue) CurrentTurnIndex = 0;
        else if (currentCombatantId.HasValue) CurrentTurnIndex = Array.FindIndex(OrderedCombatants(), x => x.Id == currentCombatantId.Value);
    }

    public void Start()
    {
        EnsurePreparing();
        if (_combatants.Count == 0 || _combatants.Any(x => x.Initiative is null))
        {
            throw new InvalidOperationException("Todos os combatentes precisam de iniciativa antes do início.");
        }
        if (_combatants.Any(x => x.CurrentHitPoints is null))
        {
            throw new InvalidOperationException("Todos os combatentes precisam de vida antes do início.");
        }
        Status = EncounterStatus.Active;
        Round = 1;
        CurrentTurnIndex = 0;
    }

    public void AdvanceTurn()
    {
        if (Status != EncounterStatus.Active || CurrentTurnIndex is null || _combatants.Count == 0)
            throw new InvalidOperationException("O combate não está ativo.");
        var next = CurrentTurnIndex.Value + 1;
        if (next >= _combatants.Count) { CurrentTurnIndex = 0; Round = checked(Round + 1); }
        else CurrentTurnIndex = next;
    }

    public void End(DateTimeOffset endedAt)
    {
        if (Status == EncounterStatus.Ended) return;
        Status = EncounterStatus.Ended;
        CurrentTurnIndex = null;
        EndedAt = endedAt;
    }

    private Combatant FindCombatant(Guid id) => _combatants.FirstOrDefault(x => x.Id == id)
        ?? throw new InvalidOperationException("Combatente não encontrado.");
    private void EnsurePreparing()
    {
        if (Status != EncounterStatus.Preparing) throw new InvalidOperationException("O combate já foi iniciado ou encerrado.");
    }
    private Combatant[] OrderedCombatants() => _combatants.OrderByDescending(x => x.Initiative ?? int.MinValue).ThenBy(x => x.CreationOrder).ToArray();
    private Guid? CurrentCombatantId() => Status == EncounterStatus.Active && CurrentTurnIndex is not null
        ? OrderedCombatants().ElementAtOrDefault(CurrentTurnIndex.Value)?.Id : null;
    private void RestoreCurrentTurn(Guid? combatantId)
    {
        if (Status != EncounterStatus.Active || combatantId is null) return;
        var index = Array.FindIndex(OrderedCombatants(), x => x.Id == combatantId.Value);
        CurrentTurnIndex = index >= 0 ? index : null;
    }
}