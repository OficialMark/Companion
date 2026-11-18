namespace Companion.Application.Creatures;

/// <summary>
/// Combatant names for creatures added from the library (spec 008, research R7): a single
/// creature with no namesake keeps its plain name; otherwise "Name N" with the smallest
/// numbers not already taken. A plain "Name" already in the encounter counts as number 1.
/// </summary>
public static class CreatureNaming
{
    public static IReadOnlyList<string> Next(string baseName, IEnumerable<string> existingNames, int count, int maxLength)
    {
        var trimmed = baseName.Trim();
        var existing = existingNames.Select(x => x.Trim()).ToArray();
        var used = new HashSet<int>();
        var plainTaken = false;
        foreach (var name in existing)
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)) { plainTaken = true; used.Add(1); continue; }
            if (name.Length > trimmed.Length + 1 && name.StartsWith(trimmed + " ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name[(trimmed.Length + 1)..], out var number) && number > 0)
                used.Add(number);
        }
        if (count == 1 && !plainTaken && used.Count == 0)
            return [Fit(trimmed, string.Empty, maxLength)];

        var names = new List<string>(count);
        var next = 1;
        while (names.Count < count)
        {
            while (used.Contains(next)) next++;
            used.Add(next);
            names.Add(Fit(trimmed, $" {next}", maxLength));
        }
        return names;
    }

    private static string Fit(string name, string suffix, int maxLength) =>
        name.Length + suffix.Length <= maxLength ? name + suffix : name[..(maxLength - suffix.Length)].TrimEnd() + suffix;
}
