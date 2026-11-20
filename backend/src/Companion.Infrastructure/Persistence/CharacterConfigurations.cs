using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Domain.Catalog;
using Companion.Domain.Characters;
using Companion.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Companion.Infrastructure.Persistence;

/// <summary>
/// Spec 011 tables. Validated records (sheet, item, spell, saved roll) are stored as one JSON
/// column each: the app never filters by their fields, and the record stays the single
/// definition of what is stored. Columns that are filtered or indexed stay real columns.
/// </summary>
internal static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static PropertyBuilder<T> AsJson<T>(this PropertyBuilder<T> property) where T : class
    {
        property.HasConversion(
                value => JsonSerializer.Serialize(value, Options),
                json => JsonSerializer.Deserialize<T>(json, Options)!)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        // Records holding lists and dictionaries compare by reference; compare the JSON instead.
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
            value => JsonSerializer.Serialize(value, Options).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!));
        return property;
    }
}

public sealed class CharacterSheetConfiguration : IEntityTypeConfiguration<CharacterSheet>
{
    public void Configure(EntityTypeBuilder<CharacterSheet> builder)
    {
        builder.ToTable("CharacterSheets");
        builder.HasKey(x => x.ParticipantId);
        builder.Property(x => x.ParticipantId).ValueGeneratedNever();
        builder.Property(x => x.Data).AsJson();
        builder.Property(x => x.Version).IsRowVersion();
        // The sheet belongs to the player name (OwnerKey), not to one participant: no FK, so deleting a
        // campaign or a participant never deletes the account's sheet (2026-11-20).
        builder.Property(x => x.OwnerKey).HasMaxLength(CharacterOwner.MaxKeyLength).IsRequired()
            .UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(x => x.OwnerKey).IsUnique();
    }
}

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Data).AsJson();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.ParticipantId, x.SortOrder });
        builder.HasOne<CharacterSheet>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CharacterSpellConfiguration : IEntityTypeConfiguration<CharacterSpell>
{
    public void Configure(EntityTypeBuilder<CharacterSpell> builder)
    {
        builder.ToTable("CharacterSpells");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Data).AsJson();
        builder.HasIndex(x => new { x.ParticipantId, x.SortOrder });
        builder.HasOne<CharacterSheet>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CharacterFeatureConfiguration : IEntityTypeConfiguration<CharacterFeature>
{
    public void Configure(EntityTypeBuilder<CharacterFeature> builder)
    {
        builder.ToTable("CharacterFeatures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Data).AsJson();
        builder.HasIndex(x => new { x.ParticipantId, x.SortOrder });
        builder.HasOne<CharacterSheet>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SavedRollConfiguration : IEntityTypeConfiguration<SavedRoll>
{
    public void Configure(EntityTypeBuilder<SavedRoll> builder)
    {
        builder.ToTable("SavedRolls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Data).AsJson();
        builder.HasIndex(x => new { x.ParticipantId, x.SortOrder });
        builder.HasOne<CharacterSheet>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("CatalogItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        CampaignScope(builder);
        builder.Property(x => x.Name).HasMaxLength(InventoryItemData.MaxNameLength).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(120);
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.Subcategory).HasMaxLength(80);
        builder.Property(x => x.Rarity).HasMaxLength(30);
        builder.Property(x => x.Cost).HasMaxLength(40);
        builder.Property(x => x.SearchKey).HasMaxLength(260).IsRequired();
        builder.Property(x => x.SearchText).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Template).AsJson();
        builder.HasIndex(x => new { x.IsRemovedFromCatalog, x.Category });
    }

    /// <summary>
    /// Spec 013, same shape as creatures (spec 008): the slug is unique among global rows only (campaign rows
    /// get a generated one), a campaign has at most one copy of each global row, and deleting a campaign
    /// deletes its rows. The base FK is NoAction: global rows are never deleted, only marked removed.
    /// </summary>
    internal static void CampaignScope<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.HasIndex("Slug").IsUnique().HasFilter("[CampaignId] IS NULL");
        builder.HasIndex("CampaignId", "BaseId").IsUnique().HasFilter("[CampaignId] IS NOT NULL AND [BaseId] IS NOT NULL");
        // The filtered index above cannot serve "WHERE CampaignId = @c" (every catalog read, campaign delete).
        builder.HasIndex("CampaignId");
        builder.HasOne<Campaign>().WithMany().HasForeignKey("CampaignId").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<T>().WithMany().HasForeignKey("BaseId").OnDelete(DeleteBehavior.NoAction);
        builder.Ignore("Origin");
    }
}

public sealed class CatalogSpellConfiguration : IEntityTypeConfiguration<CatalogSpell>
{
    public void Configure(EntityTypeBuilder<CatalogSpell> builder)
    {
        builder.ToTable("CatalogSpells");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        CatalogItemConfiguration.CampaignScope(builder);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(CatalogItem.MaxNameEnLength);
        builder.Property(x => x.School).HasMaxLength(80);
        builder.Property(x => x.SearchKey).HasMaxLength(260).IsRequired();
        builder.Property(x => x.SearchText).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.HigherLevels).HasMaxLength(4000);
        builder.Property(x => x.Template).AsJson();
        builder.Property(x => x.Classes).HasConversion(
                value => JsonSerializer.Serialize(value, JsonColumn.Options),
                json => JsonSerializer.Deserialize<string[]>(json, JsonColumn.Options) ?? Array.Empty<string>())
            .HasMaxLength(400)
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                (left, right) => left!.SequenceEqual(right!),
                value => string.Join('|', value).GetHashCode(StringComparison.Ordinal),
                value => value.ToArray()));
        builder.HasIndex(x => new { x.IsRemovedFromCatalog, x.Level });
    }
}
