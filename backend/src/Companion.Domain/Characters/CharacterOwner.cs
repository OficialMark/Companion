using Companion.Domain.Models;

namespace Companion.Domain.Characters;

/// <summary>Who owns a character sheet: one account per player name, across campaigns (2026-11-20).</summary>
public static class CharacterOwner
{
    public const int MaxKeyLength = 80;

    /// <summary>The participant's name key, or the normalized display name for legacy rows without one.</summary>
    public static string Key(Participant participant)
    {
        if (!string.IsNullOrEmpty(participant.NameKey)) return participant.NameKey;
        try { return PersonName.Key(participant.DisplayName); }
        catch (ArgumentException) { return participant.DisplayName.Trim().ToUpperInvariant(); }
    }
}
