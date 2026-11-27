namespace Companion.Domain.Models;

/// <summary>
/// A token any participant places on the board (spec 016). Free-text by default; it may also come from
/// the creature compendium (<see cref="CreatureId"/>: image and size) or the item catalog
/// (<see cref="CatalogItemId"/>: an object on the map). At most one source.
/// </summary>
public sealed class BattlemapMarker
{
    public const int MaxLabelLength = 40;

    private BattlemapMarker() { }

    public BattlemapMarker(Guid id, Guid battlemapId, Guid ownerParticipantId, string label, DateTimeOffset createdAt,
        Guid? creatureId = null, Guid? catalogItemId = null)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaxLabelLength)
        {
            throw new ArgumentException("O nome do token deve ter entre 1 e 40 caracteres.", nameof(label));
        }
        if (creatureId is not null && catalogItemId is not null)
        {
            throw new ArgumentException("O token vem de uma criatura ou de um item, não dos dois.", nameof(catalogItemId));
        }

        Id = id;
        BattlemapId = battlemapId;
        OwnerParticipantId = ownerParticipantId;
        Label = label.Trim();
        CreatedAt = createdAt;
        CreatureId = creatureId;
        CatalogItemId = catalogItemId;
    }

    /// <summary>A catalog name longer than a label is cut on a word boundary when it can be.</summary>
    public static string LabelFrom(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length <= MaxLabelLength) return trimmed;
        var cut = trimmed[..MaxLabelLength];
        var space = cut.LastIndexOf(' ');
        return (space >= MaxLabelLength / 2 ? cut[..space] : cut).TrimEnd();
    }

    public Guid Id { get; private set; }
    public Guid BattlemapId { get; private set; }
    public Guid OwnerParticipantId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Compendium creature the token was taken from; null for free-text and item tokens.</summary>
    public Guid? CreatureId { get; private set; }
    /// <summary>Catalog item the token represents; null for free-text and creature tokens.</summary>
    public Guid? CatalogItemId { get; private set; }
}
