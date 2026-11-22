using Companion.Domain.Models;
using Companion.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Companion.Infrastructure.Persistence;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
    }
}

public sealed class CampaignAccessCodeConfiguration : IEntityTypeConfiguration<CampaignAccessCode>
{
    public void Configure(EntityTypeBuilder<CampaignAccessCode> builder)
    {
        builder.ToTable("CampaignAccessCodes");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(4).IsFixedLength().IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.Role }).IsUnique();
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.CurrentHitPoints);
        builder.Property(x => x.MaxHitPoints);
        builder.Property(x => x.IsHiddenFromPanel).HasDefaultValue(false);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.DisplayName });
        // Identidade da conta (spec 011, FR-042). Já vem em maiúsculas invariantes, então a comparação é
        // binária: o banco decide igual ao C# (diferencia acentos, não depende da collation do servidor).
        // O filtro aceita os nomes repetidos antigos, que ficam com NameKey nula (FR-044).
        builder.Property(x => x.NameKey).HasMaxLength(40).UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(x => new { x.CampaignId, x.Role, x.NameKey }).IsUnique()
            .HasFilter("[NameKey] IS NOT NULL");
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParticipantSessionConfiguration : IEntityTypeConfiguration<ParticipantSession>
{
    public void Configure(EntityTypeBuilder<ParticipantSession> builder)
    {
        builder.ToTable("ParticipantSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(ParticipantSession.TokenHashLength).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ParticipantId);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParticipantAvatarConfiguration : IEntityTypeConfiguration<ParticipantAvatar>
{
    public void Configure(EntityTypeBuilder<ParticipantAvatar> builder)
    {
        builder.ToTable("ParticipantAvatars");
        builder.HasKey(x => x.ParticipantId);
        builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.MediaType).HasMaxLength(32).IsRequired();
        builder.HasOne<Participant>().WithOne().HasForeignKey<ParticipantAvatar>(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiceRollConfiguration : IEntityTypeConfiguration<DiceRoll>
{
    public void Configure(EntityTypeBuilder<DiceRoll> builder)
    {
        builder.ToTable("DiceRolls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Expression).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(DiceRoll.MaxLabelLength);
        builder.Property(x => x.ValuesJson).IsRequired();
        builder.Property(x => x.RevealedAt);
        builder.HasIndex(x => new { x.ParticipantId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.CreatedAt });
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasMany(x => x.Combatants).WithOne().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Combatants).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.CampaignId, x.CreatedAt });
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CombatantConfiguration : IEntityTypeConfiguration<Combatant>
{
    public void Configure(EntityTypeBuilder<Combatant> builder)
    {
        builder.ToTable("Combatants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.IsHitPointsHidden);
        builder.Ignore(x => x.IsCreature);
        builder.HasOne<Creature>().WithMany().HasForeignKey(x => x.CreatureId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.EncounterId, x.CreationOrder }).IsUnique();
        builder.HasIndex(x => new { x.EncounterId, x.ParticipantId }).IsUnique()
            .HasFilter("[ParticipantId] IS NOT NULL");
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CombatAttackConfiguration : IEntityTypeConfiguration<CombatAttack>
{
    public void Configure(EntityTypeBuilder<CombatAttack> builder)
    {
        builder.ToTable("CombatAttacks");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AuthorParticipantId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => x.DiceRollId).IsUnique();
        builder.HasOne<Encounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.AuthorParticipantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<DiceRoll>().WithOne().HasForeignKey<CombatAttack>(x => x.DiceRollId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CombatAttackTargetConfiguration : IEntityTypeConfiguration<CombatAttackTarget>
{
    public void Configure(EntityTypeBuilder<CombatAttackTarget> builder)
    {
        builder.ToTable("CombatAttackTargets");
        builder.HasKey(x => new { x.AttackId, x.CombatantId });
        builder.HasOne<CombatAttack>().WithMany().HasForeignKey(x => x.AttackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Combatant>().WithMany().HasForeignKey(x => x.CombatantId).OnDelete(DeleteBehavior.NoAction);
    }
}
public sealed class BattlemapConfiguration : IEntityTypeConfiguration<Battlemap>
{
    public void Configure(EntityTypeBuilder<Battlemap> builder)
    {
        builder.ToTable("Battlemaps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ImageContent).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.ImageMediaType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ImageFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IsGridVisible).HasDefaultValue(true);
        builder.Property(x => x.FogMask).HasColumnType($"varbinary({Battlemap.MaxFogMaskBytes})");
        builder.Ignore(x => x.FogMaskLength);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => x.EncounterId).IsUnique();
        builder.HasOne<Encounter>().WithOne().HasForeignKey<Battlemap>(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BattlemapTokenPositionConfiguration : IEntityTypeConfiguration<BattlemapTokenPosition>
{
    public void Configure(EntityTypeBuilder<BattlemapTokenPosition> builder)
    {
        builder.ToTable("BattlemapTokenPositions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.BattlemapId, x.ParticipantId }).IsUnique().HasFilter("[ParticipantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.CombatantId }).IsUnique().HasFilter("[CombatantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.MarkerId }).IsUnique().HasFilter("[MarkerId] IS NOT NULL");
        builder.HasIndex(x => x.BattlemapId);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_BattlemapTokenPositions_Owner",
            "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        builder.HasOne<Battlemap>().WithMany().HasForeignKey(x => x.BattlemapId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Combatant>().WithMany().HasForeignKey(x => x.CombatantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<BattlemapMarker>().WithMany().HasForeignKey(x => x.MarkerId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class BattlemapMarkerConfiguration : IEntityTypeConfiguration<BattlemapMarker>
{
    public void Configure(EntityTypeBuilder<BattlemapMarker> builder)
    {
        builder.ToTable("BattlemapMarkers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.BattlemapId);
        builder.HasOne<Battlemap>().WithMany().HasForeignKey(x => x.BattlemapId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.OwnerParticipantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class BattlemapTokenAppearanceConfiguration : IEntityTypeConfiguration<BattlemapTokenAppearance>
{
    public void Configure(EntityTypeBuilder<BattlemapTokenAppearance> builder)
    {
        builder.ToTable("BattlemapTokenAppearances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LastRollAbility).HasMaxLength(3);
        builder.Property(x => x.Version).IsRowVersion();
        var statusConditions = builder.Property(x => x.StatusConditions)
            .HasField("_statusConditions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                list => string.Join(',', list),
                csv => string.IsNullOrEmpty(csv)
                    ? new List<StatusCondition>()
                    : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<StatusCondition>).ToList())
            .HasColumnName("StatusConditions")
            .HasMaxLength(512);
        statusConditions.Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<StatusCondition>>(
            (left, right) => (left ?? Array.Empty<StatusCondition>()).SequenceEqual(right ?? Array.Empty<StatusCondition>()),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToList()));
        builder.HasIndex(x => new { x.BattlemapId, x.ParticipantId }).IsUnique().HasFilter("[ParticipantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.CombatantId }).IsUnique().HasFilter("[CombatantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.MarkerId }).IsUnique().HasFilter("[MarkerId] IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_BattlemapTokenAppearances_Owner",
            "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        builder.HasOne<Battlemap>().WithMany().HasForeignKey(x => x.BattlemapId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Combatant>().WithMany().HasForeignKey(x => x.CombatantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<BattlemapMarker>().WithMany().HasForeignKey(x => x.MarkerId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class AttackAreaConfiguration : IEntityTypeConfiguration<AttackArea>
{
    public void Configure(EntityTypeBuilder<AttackArea> builder)
    {
        builder.ToTable("AttackAreas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Shape).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.BattlemapId, x.ParticipantId }).IsUnique().HasFilter("[ParticipantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.CombatantId }).IsUnique().HasFilter("[CombatantId] IS NOT NULL");
        builder.HasIndex(x => new { x.BattlemapId, x.MarkerId }).IsUnique().HasFilter("[MarkerId] IS NOT NULL");
        builder.HasIndex(x => x.BattlemapId);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_AttackAreas_Owner",
            "(CASE WHEN [ParticipantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [CombatantId] IS NOT NULL THEN 1 ELSE 0 END) + " +
            "(CASE WHEN [MarkerId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        builder.HasOne<Battlemap>().WithMany().HasForeignKey(x => x.BattlemapId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Combatant>().WithMany().HasForeignKey(x => x.CombatantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<BattlemapMarker>().WithMany().HasForeignKey(x => x.MarkerId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CreatureConfiguration : IEntityTypeConfiguration<Creature>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Creature> builder)
    {
        builder.ToTable("Creatures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(120);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(120);
        builder.Property(x => x.SearchKey).HasMaxLength(260).IsRequired();
        builder.Property(x => x.SourceBook).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(300);
        builder.Property(x => x.Size).HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.Type).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Alignment).HasMaxLength(80);
        builder.Property(x => x.ArmorClassNote).HasMaxLength(120);
        builder.Property(x => x.HitDice).HasMaxLength(40);
        builder.Property(x => x.Speed).HasMaxLength(200);
        foreach (var name in new[] { nameof(Creature.SavingThrows), nameof(Creature.Skills), nameof(Creature.DamageVulnerabilities),
                     nameof(Creature.DamageResistances), nameof(Creature.DamageImmunities), nameof(Creature.ConditionImmunities),
                     nameof(Creature.Senses), nameof(Creature.Languages) })
            builder.Property<string?>(name).HasMaxLength(500);
        builder.Property(x => x.ChallengeRating).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ChallengeValue).HasPrecision(6, 3);
        builder.Property(x => x.LegendaryActionsIntro).HasMaxLength(2000);
        builder.Property(x => x.Notes).HasMaxLength(Creature.MaxNotesLength);
        builder.Property(x => x.ImageSourceUrl).HasMaxLength(400);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Ignore(x => x.IsCompendium);
        builder.Ignore(x => x.FootprintSize);
        builder.Ignore(x => x.InitiativeModifier);
        var comparer = new ValueComparer<List<CreatureAction>>(
            (left, right) => (left ?? new List<CreatureAction>()).SequenceEqual(right ?? new List<CreatureAction>()),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToList());
        foreach (var name in new[] { nameof(Creature.Traits), nameof(Creature.Actions), nameof(Creature.BonusActions),
                     nameof(Creature.Reactions), nameof(Creature.LegendaryActions) })
        {
            builder.Property<List<CreatureAction>>(name)
                .HasConversion(
                    list => System.Text.Json.JsonSerializer.Serialize(list, Json),
                    json => System.Text.Json.JsonSerializer.Deserialize<List<CreatureAction>>(json, Json) ?? new List<CreatureAction>())
                .HasColumnType("nvarchar(max)")
                .Metadata.SetValueComparer(comparer);
        }
        builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[CampaignId] IS NULL AND [Slug] IS NOT NULL");
        builder.HasIndex(x => new { x.CampaignId, x.BaseCreatureId }).IsUnique()
            .HasFilter("[CampaignId] IS NOT NULL AND [BaseCreatureId] IS NOT NULL");
        builder.HasIndex(x => new { x.CampaignId, x.SearchKey });
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Creature>().WithMany().HasForeignKey(x => x.BaseCreatureId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CreatureImageConfiguration : IEntityTypeConfiguration<CreatureImage>
{
    public void Configure(EntityTypeBuilder<CreatureImage> builder)
    {
        builder.ToTable("CreatureImages");
        builder.HasKey(x => x.CreatureId);
        builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.MediaType).HasMaxLength(32).IsRequired();
        builder.HasOne<Creature>().WithOne().HasForeignKey<CreatureImage>(x => x.CreatureId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SeedVersionConfiguration : IEntityTypeConfiguration<SeedVersion>
{
    public void Configure(EntityTypeBuilder<SeedVersion> builder)
    {
        builder.ToTable("SeedVersions");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(80);
        builder.Property(x => x.Hash).HasMaxLength(64).IsRequired();
    }
}

public sealed class CampaignKnowledgeEntryConfiguration : IEntityTypeConfiguration<CampaignKnowledgeEntry>
{
    public void Configure(EntityTypeBuilder<CampaignKnowledgeEntry> builder)
    {
        builder.ToTable("CampaignKnowledgeEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(CampaignKnowledgeEntry.MaxTitleLength).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(CampaignKnowledgeEntry.MaxSummaryLength);
        builder.Property(x => x.SharedDescription).HasMaxLength(CampaignKnowledgeEntry.MaxDescriptionLength);
        builder.Property(x => x.ImageContent).HasColumnType("varbinary(max)");
        builder.Property(x => x.ImageMediaType).HasMaxLength(32);
        builder.Property(x => x.ImageFileName).HasMaxLength(255);
        builder.Property(x => x.PdfContent).HasColumnType("varbinary(max)");
        builder.Property(x => x.PdfFileName).HasMaxLength(255);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.CampaignId, x.Kind, x.IsPlayerVisible, x.Title });
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CampaignKnowledgeContributionConfiguration : IEntityTypeConfiguration<CampaignKnowledgeContribution>
{
    public void Configure(EntityTypeBuilder<CampaignKnowledgeContribution> builder)
    {
        builder.ToTable("CampaignKnowledgeContributions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(8_000).IsRequired();
        builder.HasIndex(x => new { x.EntryId, x.CreatedAt });
        builder.HasOne<CampaignKnowledgeEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CampaignKnowledgeMasterNoteConfiguration : IEntityTypeConfiguration<CampaignKnowledgeMasterNote>
{
    public void Configure(EntityTypeBuilder<CampaignKnowledgeMasterNote> builder)
    {
        builder.ToTable("CampaignKnowledgeMasterNotes");
        builder.HasKey(x => x.EntryId);
        builder.Property(x => x.Body).HasMaxLength(12_000).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne<CampaignKnowledgeEntry>().WithOne().HasForeignKey<CampaignKnowledgeMasterNote>(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CampaignMapPointConfiguration : IEntityTypeConfiguration<CampaignMapPoint>
{
    public void Configure(EntityTypeBuilder<CampaignMapPoint> builder)
    {
        builder.ToTable("CampaignMapPoints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2_000);
        builder.Property(x => x.X).HasPrecision(8, 7);
        builder.Property(x => x.Y).HasPrecision(8, 7);
        builder.HasIndex(x => new { x.MapEntryId, x.CreatedAt });
        builder.HasOne<CampaignKnowledgeEntry>().WithMany().HasForeignKey(x => x.MapEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}
