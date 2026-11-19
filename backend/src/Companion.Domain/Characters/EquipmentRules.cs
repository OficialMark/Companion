namespace Companion.Domain.Characters;

public sealed record EquipChange(Guid ItemId, bool Equipped);

public sealed record EquipResult(IReadOnlyList<EquipChange> Changes, IReadOnlyList<string> Notices);

/// <summary>
/// Equip limits (spec 011, FR-017): at most one armor and one shield equipped; any number of
/// weapons and gear. The bag itself has no limit. Equipping a second armor (or shield) unequips
/// the previous one of the same category and says so.
/// </summary>
public static class EquipmentRules
{
    public static EquipResult SetEquipped(IReadOnlyList<ItemEntry> items, Guid itemId, bool equipped)
    {
        var target = items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new ArgumentException("Item não encontrado na sua ficha.");
        if (equipped && !InventoryItemData.IsEquippable(target.Data.Category))
            throw new ArgumentException($"{target.Data.Name} não pode ser equipado.");

        var changes = new List<EquipChange>();
        var notices = new List<string>();
        if (target.Data.Equipped != equipped) changes.Add(new EquipChange(target.Id, equipped));
        if (!equipped || target.Data.Category is not (ItemCategory.Armor or ItemCategory.Shield))
            return new EquipResult(changes, notices);

        foreach (var other in items.Where(x => x.Id != itemId && x.Data.Equipped && x.Data.Category == target.Data.Category))
        {
            changes.Add(new EquipChange(other.Id, false));
            notices.Add($"{other.Data.Name} foi desequipado.");
        }

        return new EquipResult(changes, notices);
    }

    /// <summary>
    /// Repairs a list that breaks the limits (two armors equipped by a concurrent write):
    /// keeps the first by order in each limited category.
    /// </summary>
    public static IReadOnlyList<EquipChange> Repair(IReadOnlyList<ItemEntry> items) =>
        items.Where(x => x.Data.Equipped && x.Data.Category is ItemCategory.Armor or ItemCategory.Shield)
            .GroupBy(x => x.Data.Category)
            .SelectMany(group => group.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Skip(1))
            .Select(x => new EquipChange(x.Id, false))
            .ToArray();
}
