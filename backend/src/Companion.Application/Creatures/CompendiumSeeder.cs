using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Application.Contracts;
using Companion.Domain.Models;

namespace Companion.Application.Creatures;

/// <summary>Raw compendium seed (the JSON written by scripts/import-creatures).</summary>
public interface ICompendiumSeedSource
{
    string Key { get; }
    byte[] Read();
}

/// <summary>
/// Loads the global creature compendium (spec 008, research R4). Idempotent: when the seed's
/// SHA-256 matches the last applied one it is a no-op; otherwise it upserts by slug and marks
/// entries that disappeared from the seed as removed (combatants may still point at them).
/// </summary>
public sealed class CompendiumSeeder(ICompanionDbContext db, ICompendiumSeedSource source, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var bytes = source.Read();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var version = db.SeedVersions.SingleOrDefault(x => x.Key == source.Key);
        if (version?.Hash == hash) return 0;

        var seed = JsonSerializer.Deserialize<SeedFile>(bytes, Json)
            ?? throw new InvalidOperationException("Seed do compêndio vazio.");
        var now = timeProvider.GetUtcNow();
        var existing = db.Creatures.Where(x => x.CampaignId == null && x.Slug != null).ToDictionary(x => x.Slug!);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changed = 0;
        foreach (var entry in seed.Creatures)
        {
            if (!seen.Add(entry.Slug)) continue;
            var stats = ToStats(entry);
            if (existing.TryGetValue(entry.Slug, out var creature))
                creature.RefreshFromCompendium(entry.SourceBook, entry.SourceUrl, entry.ImageUrl, stats, now);
            else
                db.Add(Creature.ForCompendium(Guid.NewGuid(), entry.Slug, entry.SourceBook, entry.SourceUrl, entry.ImageUrl, stats, now));
            changed++;
        }
        foreach (var (slug, creature) in existing)
            if (!seen.Contains(slug) && !creature.IsRemovedFromCompendium) creature.MarkRemovedFromCompendium(now);

        if (version is null) db.Add(new SeedVersion(source.Key, hash, now));
        else version.Update(hash, now);
        await db.SaveChangesAsync(cancellationToken);
        return changed;
    }

    internal static CreatureStats ToStats(SeedCreature x) => new(
        DisplayName(x.Name, x.NameEn), x.NameEn, x.Size, x.Type, x.Alignment, x.ArmorClass, x.ArmorClassNote,
        Math.Max(1, x.HitPoints), x.HitDice, x.Speed, x.Initiative is null ? null : Math.Clamp(x.Initiative.Value, -20, 30),
        x.Abilities.Str, x.Abilities.Dex, x.Abilities.Con, x.Abilities.Int, x.Abilities.Wis, x.Abilities.Cha,
        x.SavingThrows, x.Skills, x.DamageVulnerabilities, x.DamageResistances, x.DamageImmunities, x.ConditionImmunities,
        x.Senses, x.Languages, x.ChallengeRating, x.ExperiencePoints, x.ProficiencyBonus,
        Actions(x.Traits), Actions(x.Actions), Actions(x.BonusActions), Actions(x.Reactions), Actions(x.LegendaryActions),
        x.LegendaryActionsIntro);

    /// <summary>The site shows "Nome PT (English Name)"; the English name has its own field.</summary>
    public static string DisplayName(string name, string? nameEn)
    {
        var trimmed = name.Trim();
        if (!string.IsNullOrWhiteSpace(nameEn))
        {
            var suffix = $" ({nameEn.Trim()})";
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && trimmed.Length > suffix.Length)
                trimmed = trimmed[..^suffix.Length].TrimEnd();
        }
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static List<CreatureAction> Actions(IReadOnlyList<SeedAction>? items) =>
        (items ?? []).Take(Creature.MaxListItems)
            .Select(x => new CreatureAction(Clip(x.Name, 120), Clip(x.Description, 4000)))
            .ToList();

    private static string Clip(string? value, int max)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > max ? text[..max] : text;
    }

    internal sealed record SeedFile(string Source, string? Attribution, IReadOnlyList<SeedCreature> Creatures);

    internal sealed record SeedCreature(
        string Slug, string Name, string? NameEn, string SourceBook, string? SourceUrl, CreatureSize Size, string Type,
        string? Alignment, int ArmorClass, string? ArmorClassNote, int HitPoints, string? HitDice, string? Speed, int? Initiative,
        SeedAbilities Abilities, string? SavingThrows, string? Skills, string? DamageVulnerabilities, string? DamageResistances,
        string? DamageImmunities, string? ConditionImmunities, string? Senses, string? Languages, string ChallengeRating,
        int? ExperiencePoints, int? ProficiencyBonus, IReadOnlyList<SeedAction>? Traits, IReadOnlyList<SeedAction>? Actions,
        IReadOnlyList<SeedAction>? BonusActions, IReadOnlyList<SeedAction>? Reactions, IReadOnlyList<SeedAction>? LegendaryActions,
        string? LegendaryActionsIntro, string? ImageUrl);

    internal sealed record SeedAbilities(int Str, int Dex, int Con, int Int, int Wis, int Cha);
    internal sealed record SeedAction(string Name, string? Description);
}
