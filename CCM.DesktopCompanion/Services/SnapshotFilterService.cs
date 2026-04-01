using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.Services;

internal sealed class SnapshotFilterService
{
    public IReadOnlyList<CooldownRecord> ApplyFilters(DesktopSnapshot snapshot, DesktopCompanionSettings settings)
    {
        return snapshot.Cooldowns
            .Where(cooldown => cooldown.Enabled)
            .Where(cooldown => Matches(settings.SelectedCharacters, cooldown.GetCharacterDisplayName()))
            .Where(cooldown => Matches(settings.SelectedProfessions, cooldown.Profession))
            .Where(cooldown => cooldown.IsConcentrationOnly || Matches(settings.SelectedExpansions, NormalizeExpansion(cooldown.Expansion)))
            .Where(cooldown => cooldown.IsConcentrationOnly || Matches(settings.SelectedItems, cooldown.ItemName))
            .ToList();
    }

    public IReadOnlyList<string> GetCharacterOptions(DesktopSnapshot snapshot)
    {
        return snapshot.Cooldowns
            .Select(cooldown => cooldown.GetCharacterDisplayName())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetProfessionOptions(DesktopSnapshot snapshot)
    {
        return snapshot.Cooldowns
            .Select(cooldown => cooldown.Profession)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetItemOptions(DesktopSnapshot snapshot)
    {
        return snapshot.Cooldowns
            .Where(cooldown => !cooldown.IsConcentrationOnly)
            .Select(cooldown => cooldown.ItemName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetExpansionOptions(DesktopSnapshot snapshot)
    {
        return snapshot.Cooldowns
            .Where(cooldown => !cooldown.IsConcentrationOnly)
            .Select(cooldown => NormalizeExpansion(cooldown.Expansion))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Matches(HashSet<string> selectedValues, string candidate)
    {
        return selectedValues.Contains(candidate);
    }

    private static string NormalizeExpansion(string expansion)
    {
        return string.IsNullOrWhiteSpace(expansion) ? "Unknown" : expansion;
    }
}
