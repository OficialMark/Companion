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

    public Combatant AddCombatant(Guid id, string name, int modifier, Guid? participantId = null)
    {
        EnsurePreparing();
        if (participantId.HasValue && _combatants.Any(x => x.ParticipantId == participantId))
        {
            throw new InvalidOperationException("Este participante já está na iniciativa.");
        }

        var combatant = new Combatant(id, Id, name, modifier, _combatants.Count, participantId);
        _combatants.Add(combatant);
        return combatant;
    }

    public void UpdateCombatant(Guid combatantId, string name, int modifier)
    {
        var currentId = CurrentCombatantId();
        var combatant = _combatants.FirstOrDefault(x => x.Id == combatantId)
            ?? throw new InvalidOperationException("Combatente não encontrado.");
        combatant.Update(name, modifier);
        RestoreCurrentTurn(currentId);
    }

    public void RemoveCombatant(Guid combatantId)
    {
        var ordered = OrderedCombatants();
        var removedIndex = Array.FindIndex(ordered, x => x.Id == combatantId);
        if (removedIndex < 0) throw new InvalidOperationException("Combatente não encontrado.");

        var currentId = CurrentCombatantId();
        var removedCurrent = currentId == combatantId;
        Guid? successorId = null;
        if (removedCurrent && ordered.Length > 1)
        {
            successorId = ordered[(removedIndex + 1) % ordered.Length].Id;
        }

        _combatants.RemoveAll(x => x.Id == combatantId);
        RestoreCurrentTurn(removedCurrent ? successorId : currentId);
        if (Status == EncounterStatus.Active && _combatants.Count == 0)
        {
            CurrentTurnIndex = null;
        }
    }

    public void RollInitiative(Func<int> rollD20, Guid? combatantId = null)
    {
        if (Status == EncounterStatus.Ended)
        {
            throw new InvalidOperationException("O combate já foi encerrado.");
        }

        var currentCombatantId = Status == EncounterStatus.Active && CurrentTurnIndex is not null
            ? OrderedCombatants().ElementAtOrDefault(CurrentTurnIndex.Value)?.Id
            : null;
        var targets = combatantId.HasValue
            ? _combatants.Where(x => x.Id == combatantId.Value)
            : _combatants;

        var materialized = targets.ToArray();
        if (combatantId.HasValue && materialized.Length == 0)
        {
            throw new InvalidOperationException("Combatente não encontrado.");
        }

        foreach (var combatant in materialized)
        {
            combatant.SetInitiative(rollD20());
        }

        if (currentCombatantId.HasValue)
        {
            CurrentTurnIndex = Array.FindIndex(OrderedCombatants(), x => x.Id == currentCombatantId.Value);
        }
    }

    public void Start()
    {
        EnsurePreparing();
        if (_combatants.Count == 0 || _combatants.Any(x => x.Initiative is null))
        {
            throw new InvalidOperationException("Todos os combatentes precisam de iniciativa antes do início.");
        }

        Status = EncounterStatus.Active;
        Round = 1;
        CurrentTurnIndex = 0;
    }

    public void AdvanceTurn()
    {
        if (Status != EncounterStatus.Active || CurrentTurnIndex is null || _combatants.Count == 0)
        {
            throw new InvalidOperationException("O combate não está ativo.");
        }

        var next = CurrentTurnIndex.Value + 1;
        if (next >= _combatants.Count)
        {
            CurrentTurnIndex = 0;
            Round = checked(Round + 1);
        }
        else
        {
            CurrentTurnIndex = next;
        }
    }

    public void End(DateTimeOffset endedAt)
    {
        if (Status == EncounterStatus.Ended)
        {
            return;
        }

        Status = EncounterStatus.Ended;
        CurrentTurnIndex = null;
        EndedAt = endedAt;
    }

    private void EnsurePreparing()
    {
        if (Status != EncounterStatus.Preparing)
        {
            throw new InvalidOperationException("O combate já foi iniciado ou encerrado.");
        }
    }

    private Combatant[] OrderedCombatants() => _combatants
        .OrderByDescending(x => x.Initiative ?? int.MinValue)
        .ThenBy(x => x.CreationOrder)
        .ToArray();

    private Guid? CurrentCombatantId() => Status == EncounterStatus.Active && CurrentTurnIndex is not null
        ? OrderedCombatants().ElementAtOrDefault(CurrentTurnIndex.Value)?.Id
        : null;

    private void RestoreCurrentTurn(Guid? combatantId)
    {
        if (Status != EncounterStatus.Active || combatantId is null) return;
        var index = Array.FindIndex(OrderedCombatants(), x => x.Id == combatantId.Value);
        CurrentTurnIndex = index >= 0 ? index : null;
    }
}
