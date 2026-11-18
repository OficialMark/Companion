using System.Globalization;
using System.Text;

namespace Companion.Domain.Models;

public enum CreatureSize { Tiny, Small, Medium, Large, Huge, Gargantuan }

/// <summary>Ability keys as stored in roll presets and exchanged with the client.</summary>
public static class Ability
{
    public const string Strength = "str", Dexterity = "dex", Constitution = "con",
        Intelligence = "int", Wisdom = "wis", Charisma = "cha";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        { Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma };
}

public sealed record CreatureAction(string Name, string Description);

/// <summary>Every editable stat-block field, shared by the compendium seed and the master's edits.</summary>
public sealed record CreatureStats(
    string Name,
    string? NameEn,
    CreatureSize Size,
    string Type,
    string? Alignment,
    int ArmorClass,
    string? ArmorClassNote,
    int HitPoints,
    string? HitDice,
    string? Speed,
    int? Initiative,
    int Strength, int Dexterity, int Constitution, int Intelligence, int Wisdom, int Charisma,
    string? SavingThrows,
    string? Skills,
    string? DamageVulnerabilities,
    string? DamageResistances,
    string? DamageImmunities,
    string? ConditionImmunities,
    string? Senses,
    string? Languages,
    string ChallengeRating,
    int? ExperiencePoints,
    int? ProficiencyBonus,
    IReadOnlyList<CreatureAction> Traits,
    IReadOnlyList<CreatureAction> Actions,
    IReadOnlyList<CreatureAction> BonusActions,
    IReadOnlyList<CreatureAction> Reactions,
    IReadOnlyList<CreatureAction> LegendaryActions,
    string? LegendaryActionsIntro);

/// <summary>
/// A creature stat block (spec 008). <see cref="CampaignId"/> null = global compendium entry
/// (seeded, never edited in place). A campaign row is either the master's own creature or,
/// when <see cref="BaseCreatureId"/> is set, the campaign's edited copy of a compendium entry
/// that shadows it for that campaign only (copy-on-write; deleting it restores the original).
/// </summary>
public sealed class Creature
{
    public const int MaxListItems = 40;
    public const int MaxNotesLength = 4000;
    public const string CampaignSourceBook = "Mesa";

    private Creature() { }

    private Creature(Guid id, Guid? campaignId, Guid? baseCreatureId, string? slug, string sourceBook, string? sourceUrl,
        string? imageSourceUrl, DateTimeOffset now)
    {
        Id = id;
        CampaignId = campaignId;
        BaseCreatureId = baseCreatureId;
        Slug = slug;
        SourceBook = sourceBook;
        SourceUrl = sourceUrl;
        ImageSourceUrl = imageSourceUrl;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid? CampaignId { get; private set; }
    public Guid? BaseCreatureId { get; private set; }
    public string? Slug { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public string SearchKey { get; private set; } = string.Empty;
    public string SourceBook { get; private set; } = string.Empty;
    public string? SourceUrl { get; private set; }
    public CreatureSize Size { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string? Alignment { get; private set; }
    public int ArmorClass { get; private set; }
    public string? ArmorClassNote { get; private set; }
    public int HitPoints { get; private set; }
    public string? HitDice { get; private set; }
    public string? Speed { get; private set; }
    public int? Initiative { get; private set; }
    public int Strength { get; private set; }
    public int Dexterity { get; private set; }
    public int Constitution { get; private set; }
    public int Intelligence { get; private set; }
    public int Wisdom { get; private set; }
    public int Charisma { get; private set; }
    public string? SavingThrows { get; private set; }
    public string? Skills { get; private set; }
    public string? DamageVulnerabilities { get; private set; }
    public string? DamageResistances { get; private set; }
    public string? DamageImmunities { get; private set; }
    public string? ConditionImmunities { get; private set; }
    public string? Senses { get; private set; }
    public string? Languages { get; private set; }
    public string ChallengeRating { get; private set; } = "0";
    public decimal ChallengeValue { get; private set; }
    public int? ExperiencePoints { get; private set; }
    public int? ProficiencyBonus { get; private set; }
    public List<CreatureAction> Traits { get; private set; } = [];
    public List<CreatureAction> Actions { get; private set; } = [];
    public List<CreatureAction> BonusActions { get; private set; } = [];
    public List<CreatureAction> Reactions { get; private set; } = [];
    public List<CreatureAction> LegendaryActions { get; private set; } = [];
    public string? LegendaryActionsIntro { get; private set; }
    public string? Notes { get; private set; }
    public string? ImageSourceUrl { get; private set; }
    public Guid? ImageRevision { get; private set; }
    public DateTimeOffset? ImageFetchFailedAt { get; private set; }
    public bool IsRemovedFromCompendium { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public bool IsCompendium => CampaignId is null;

    public static Creature ForCompendium(Guid id, string slug, string sourceBook, string? sourceUrl, string? imageSourceUrl,
        CreatureStats stats, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("O identificador da criatura é obrigatório.", nameof(slug));
        var creature = new Creature(id, null, null, slug.Trim(), sourceBook, sourceUrl, imageSourceUrl, now);
        creature.Apply(stats, now);
        return creature;
    }

    public static Creature ForCampaign(Guid id, Guid campaignId, CreatureStats stats, DateTimeOffset now)
    {
        var creature = new Creature(id, campaignId, null, null, CampaignSourceBook, null, null, now);
        creature.Apply(stats, now);
        return creature;
    }

    /// <summary>The campaign's own copy of a compendium entry (image link and source kept).</summary>
    public Creature CreateCampaignCopy(Guid id, Guid campaignId, DateTimeOffset now)
    {
        if (!IsCompendium) throw new InvalidOperationException("Só criaturas do compêndio geram cópias da mesa.");
        var copy = new Creature(id, campaignId, Id, null, SourceBook, SourceUrl, ImageSourceUrl, now)
        {
            ImageRevision = ImageRevision
        };
        copy.Apply(ToStats(), now);
        return copy;
    }

    public void Update(CreatureStats stats, DateTimeOffset now) => Apply(stats, now);

    /// <summary>Re-seed of a compendium entry (only the seeder calls this).</summary>
    public void RefreshFromCompendium(string sourceBook, string? sourceUrl, string? imageSourceUrl, CreatureStats stats, DateTimeOffset now)
    {
        if (!IsCompendium) throw new InvalidOperationException("Só criaturas do compêndio são atualizadas pelo seed.");
        SourceBook = sourceBook;
        SourceUrl = sourceUrl;
        if (ImageSourceUrl != imageSourceUrl) { ImageSourceUrl = imageSourceUrl; ImageFetchFailedAt = null; }
        IsRemovedFromCompendium = false;
        Apply(stats, now);
    }

    public void MarkRemovedFromCompendium(DateTimeOffset now) { IsRemovedFromCompendium = true; UpdatedAt = now; }

    public void SetNotes(string? notes, DateTimeOffset now)
    {
        if (IsCompendium) throw new InvalidOperationException("Anotações pertencem à criatura da mesa.");
        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > MaxNotesLength })
            throw new ArgumentException($"As anotações devem ter no máximo {MaxNotesLength} caracteres.", nameof(notes));
        Notes = trimmed;
        UpdatedAt = now;
    }

    public void SetImage(Guid? revision, DateTimeOffset now)
    {
        ImageRevision = revision;
        // An explicit removal by the master must not be undone by a later lazy fetch.
        if (revision is null) ImageSourceUrl = null;
        ImageFetchFailedAt = null;
        UpdatedAt = now;
    }

    public void MarkImageFetched(Guid revision) { ImageRevision = revision; ImageFetchFailedAt = null; }
    public void MarkImageFetchFailed(DateTimeOffset now) => ImageFetchFailedAt = now;

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public int FootprintSize => Size switch
    {
        CreatureSize.Large => 2,
        CreatureSize.Huge => 3,
        CreatureSize.Gargantuan => 4,
        _ => 1
    };

    /// <summary>Initiative bonus for new combatants: the stat block's own value, else the DEX modifier.</summary>
    public int InitiativeModifier => Initiative ?? Modifier(Dexterity);

    public CreatureStats ToStats() => new(Name, NameEn, Size, Type, Alignment, ArmorClass, ArmorClassNote, HitPoints, HitDice,
        Speed, Initiative, Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma, SavingThrows, Skills,
        DamageVulnerabilities, DamageResistances, DamageImmunities, ConditionImmunities, Senses, Languages,
        ChallengeRating, ExperiencePoints, ProficiencyBonus, Traits.ToList(), Actions.ToList(), BonusActions.ToList(),
        Reactions.ToList(), LegendaryActions.ToList(), LegendaryActionsIntro);

    private void Apply(CreatureStats stats, DateTimeOffset now)
    {
        Name = Required(stats.Name, 120, "O nome");
        NameEn = Optional(stats.NameEn, 120, "O nome em inglês");
        Size = Enum.IsDefined(stats.Size) ? stats.Size : throw new ArgumentException("Tamanho inválido.", nameof(stats));
        Type = Required(stats.Type, 80, "O tipo");
        Alignment = Optional(stats.Alignment, 80, "O alinhamento");
        ArmorClass = Range(stats.ArmorClass, 0, 50, "A CA");
        ArmorClassNote = Optional(stats.ArmorClassNote, 120, "A nota da CA");
        HitPoints = Range(stats.HitPoints, 1, 9999, "Os PV");
        HitDice = Optional(stats.HitDice, 40, "Os dados de vida");
        Speed = Optional(stats.Speed, 200, "O deslocamento");
        Initiative = stats.Initiative is null ? null : Range(stats.Initiative.Value, -20, 30, "A iniciativa");
        Strength = Score(stats.Strength, "FOR"); Dexterity = Score(stats.Dexterity, "DES");
        Constitution = Score(stats.Constitution, "CON"); Intelligence = Score(stats.Intelligence, "INT");
        Wisdom = Score(stats.Wisdom, "SAB"); Charisma = Score(stats.Charisma, "CAR");
        SavingThrows = Optional(stats.SavingThrows, 500, "As salvaguardas");
        Skills = Optional(stats.Skills, 500, "As perícias");
        DamageVulnerabilities = Optional(stats.DamageVulnerabilities, 500, "As vulnerabilidades");
        DamageResistances = Optional(stats.DamageResistances, 500, "As resistências");
        DamageImmunities = Optional(stats.DamageImmunities, 500, "As imunidades");
        ConditionImmunities = Optional(stats.ConditionImmunities, 500, "As imunidades a condição");
        Senses = Optional(stats.Senses, 500, "Os sentidos");
        Languages = Optional(stats.Languages, 500, "Os idiomas");
        ChallengeRating = Required(stats.ChallengeRating, 10, "O ND");
        ChallengeValue = ParseChallenge(ChallengeRating);
        ExperiencePoints = stats.ExperiencePoints is null ? null : Range(stats.ExperiencePoints.Value, 0, 1_000_000, "O XP");
        ProficiencyBonus = stats.ProficiencyBonus is null ? null : Range(stats.ProficiencyBonus.Value, 0, 20, "O bônus de proficiência");
        Traits = Actions_(stats.Traits, "Traços");
        Actions = Actions_(stats.Actions, "Ações");
        BonusActions = Actions_(stats.BonusActions, "Ações bônus");
        Reactions = Actions_(stats.Reactions, "Reações");
        LegendaryActions = Actions_(stats.LegendaryActions, "Ações lendárias");
        LegendaryActionsIntro = Optional(stats.LegendaryActionsIntro, 2000, "A introdução das ações lendárias");
        SearchKey = BuildSearchKey(Name, NameEn);
        UpdatedAt = now;
    }

    /// <summary>Lowercase, accent-free "name nameEn" so search ignores case and diacritics.</summary>
    public static string BuildSearchKey(string name, string? nameEn)
    {
        var key = Normalize($"{name} {nameEn}".Trim());
        return key.Length > 260 ? key[..260] : key;
    }

    public static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static decimal ParseChallenge(string value)
    {
        var text = value.Trim();
        var slash = text.IndexOf('/');
        if (slash > 0 && int.TryParse(text[..slash], out var numerator) && int.TryParse(text[(slash + 1)..], out var denominator) && denominator > 0)
            return Math.Round((decimal)numerator / denominator, 3);
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number is >= 0 and <= 30)
            return number;
        throw new ArgumentException($"ND inválido: {value}.", nameof(value));
    }

    private static string Required(string? value, int max, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new ArgumentException($"{label} é obrigatório.");
        if (trimmed.Length > max) throw new ArgumentException($"{label} deve ter no máximo {max} caracteres.");
        return trimmed;
    }

    private static string? Optional(string? value, int max, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max) throw new ArgumentException($"{label} deve ter no máximo {max} caracteres.");
        return trimmed;
    }

    private static int Range(int value, int min, int max, string label) =>
        value >= min && value <= max ? value : throw new ArgumentOutOfRangeException(label, $"{label} deve estar entre {min} e {max}.");

    private static int Score(int value, string label) => Range(value, 1, 30, label);

    private static List<CreatureAction> Actions_(IReadOnlyList<CreatureAction>? items, string label)
    {
        var list = (items ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Description))
            .Select(item => new CreatureAction(Required(item.Name, 120, $"O nome em {label}"), Optional(item.Description, 4000, $"A descrição em {label}") ?? string.Empty))
            .ToList();
        if (list.Count > MaxListItems) throw new ArgumentException($"{label}: no máximo {MaxListItems} itens.");
        return list;
    }
}

/// <summary>Image bytes kept apart from the stat block so listing never loads blobs.</summary>
public sealed class CreatureImage
{
    public const int MaxBytes = 1024 * 1024;

    private CreatureImage() { }

    public CreatureImage(Guid creatureId, byte[] content, string mediaType, bool fromSource)
    {
        CreatureId = creatureId;
        Replace(content, mediaType, fromSource);
    }

    public Guid CreatureId { get; private set; }
    public byte[] Content { get; private set; } = [];
    public string MediaType { get; private set; } = string.Empty;
    public Guid Revision { get; private set; }
    public bool FromSource { get; private set; }

    public void Replace(byte[] content, string mediaType, bool fromSource)
    {
        if (content.Length is < 1 or > MaxBytes) throw new ArgumentException("A imagem deve ter no máximo 1 MB.");
        Content = content.ToArray();
        MediaType = mediaType;
        FromSource = fromSource;
        Revision = Guid.NewGuid();
    }
}

public sealed class SeedVersion
{
    private SeedVersion() { }
    public SeedVersion(string key, string hash, DateTimeOffset appliedAt) { Key = key; Hash = hash; AppliedAt = appliedAt; }
    public string Key { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public DateTimeOffset AppliedAt { get; private set; }
    public void Update(string hash, DateTimeOffset appliedAt) { Hash = hash; AppliedAt = appliedAt; }
}
