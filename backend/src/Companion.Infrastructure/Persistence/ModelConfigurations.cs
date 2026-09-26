using Companion.Domain.Models;
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
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.DisplayName });
        builder.HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiceRollConfiguration : IEntityTypeConfiguration<DiceRoll>
{
    public void Configure(EntityTypeBuilder<DiceRoll> builder)
    {
        builder.ToTable("DiceRolls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Expression).HasMaxLength(128).IsRequired();
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
            .HasMaxLength(128);
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
