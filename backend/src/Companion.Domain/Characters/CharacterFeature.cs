namespace Companion.Domain.Characters;

/// <summary>When a feature's uses come back (spec 013, FR-014). A long rest also restores short-rest features.</summary>
public enum FeatureRecovery { None, ShortRest, LongRest }

public enum RestKind { Short, Long }

/// <summary>
/// A class, species or feat feature on a player's sheet: text, an optional use counter and declarative effects.
/// </summary>
public sealed record CharacterFeatureData(
    string Name,
    string? Source,
    string? Description,
    int? MaxUses,
    int CurrentUses,
    FeatureRecovery Recovery,
    IReadOnlyList<ItemEffect>? Effects = null)
{
    public const int MaxNameLength = 80;
    public const int MaxSourceLength = 60;
    public const int MaxDescriptionLength = 8000;
    public const int MaxUsesLimit = 999;

    public bool HasCounter => MaxUses is not null;

    /// <summary>Normalizes text and clamps the counter, or throws <see cref="ArgumentException"/> with a PT-BR message.</summary>
    public CharacterFeatureData Validated()
    {
        var name = Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new ArgumentException($"O nome da característica deve ter entre 1 e {MaxNameLength} caracteres.");
        var source = string.IsNullOrWhiteSpace(Source) ? null : Source.Trim();
        if (source is { Length: > MaxSourceLength })
            throw new ArgumentException($"A origem deve ter até {MaxSourceLength} caracteres.");
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        if (description is { Length: > MaxDescriptionLength })
            throw new ArgumentException($"A descrição deve ter até {MaxDescriptionLength} caracteres.");
        if (!Enum.IsDefined(Recovery)) throw new ArgumentException("Recuperação inválida.");
        if (MaxUses is < 1 or > MaxUsesLimit)
            throw new ArgumentException($"Os usos máximos devem ficar entre 1 e {MaxUsesLimit}.");
        var effects = Effects ?? [];
        if (effects.Count > ItemEffect.MaxEffectsPerItem)
            throw new ArgumentException($"Uma característica pode ter até {ItemEffect.MaxEffectsPerItem} efeitos.");

        return this with
        {
            Name = name,
            Source = source,
            Description = description,
            // Without a counter there is nothing to spend nor to recover.
            CurrentUses = MaxUses is { } max ? Math.Clamp(CurrentUses, 0, max) : 0,
            Recovery = MaxUses is null ? FeatureRecovery.None : Recovery,
            Effects = effects.Select(effect => effect.Validated()).ToArray()
        };
    }

    /// <summary>The counter moved by <paramref name="delta"/>, kept between 0 and the maximum.</summary>
    public CharacterFeatureData Spent(int delta) =>
        MaxUses is { } max ? this with { CurrentUses = Math.Clamp(CurrentUses + delta, 0, max) } : this;

    public CharacterFeatureData Restored() => MaxUses is { } max ? this with { CurrentUses = max } : this;

    /// <summary>Whether this rest restores the feature (FR-014): a long rest covers short-rest features too.</summary>
    public bool RecoversOn(RestKind rest) => Recovery switch
    {
        FeatureRecovery.ShortRest => true,
        FeatureRecovery.LongRest => rest == RestKind.Long,
        _ => false
    };
}

public sealed class CharacterFeature
{
    public const int MaxFeaturesPerCharacter = 100;

    private CharacterFeature() { }

    /// <summary>A new feature starts with every use available (FR-015).</summary>
    public CharacterFeature(Guid id, Guid participantId, int sortOrder, CharacterFeatureData data, DateTimeOffset now)
    {
        Id = id;
        ParticipantId = participantId;
        SortOrder = sortOrder;
        Data = data.Validated().Restored();
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int SortOrder { get; private set; }
    public CharacterFeatureData Data { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Replaces the fields. The counter is the server's: the stored current value is kept (the client's is
    /// ignored, so editing the text in one tab never undoes a use spent in another), capped by a lower
    /// maximum; a counter that was just turned on starts full (FR-015).
    /// </summary>
    public void Update(CharacterFeatureData data)
    {
        var next = data.Validated();
        if (next.MaxUses is { } max)
            next = next with { CurrentUses = Data.MaxUses is null ? max : Math.Min(Data.CurrentUses, max) };
        Data = next;
    }

    public void Spend(int delta) => Data = Data.Spent(delta);

    public void Restore() => Data = Data.Restored();

    /// <summary>Restores the feature when <paramref name="rest"/> covers its recovery; true when something changed.</summary>
    public bool Rest(RestKind rest)
    {
        if (!Data.RecoversOn(rest) || Data.CurrentUses == Data.MaxUses) return false;
        Restore();
        return true;
    }

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;

    public FeatureEntry ToEntry() => new(Id, SortOrder, Data);
}

/// <summary>Immutable feature projection consumed by the pure character rules.</summary>
public sealed record FeatureEntry(Guid Id, int SortOrder, CharacterFeatureData Data);
