namespace CCM.DesktopCompanion.Models;

internal sealed class DesktopSnapshot
{
    public static DesktopSnapshot Empty { get; } = new();

    public int SchemaVersion { get; set; }
    public long GeneratedAt { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public List<CharacterRecord> Characters { get; } = [];
    public List<CooldownRecord> Cooldowns { get; } = [];

    public int GetReadyCooldownCount(IEnumerable<CooldownRecord>? cooldowns = null)
    {
        var source = cooldowns ?? Cooldowns;
        return source.Count(cooldown => cooldown.Enabled && cooldown.ReadyChargesNow > 0);
    }
}

internal sealed class CharacterRecord
{
    public string CharacterKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string RealmName { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public List<string> Professions { get; } = [];
}

internal sealed class CooldownRecord
{
    public long Id { get; set; }
    public string CharacterKey { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string RealmName { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Expansion { get; set; } = string.Empty;
    public long? RecipeId { get; set; }
    public int? CurrentCharges { get; set; }
    public int? MaxCharges { get; set; }
    public int DurationSeconds { get; set; }
    public long ReadyTime { get; set; }
    public bool Enabled { get; set; } = true;
    public string Source { get; set; } = string.Empty;

    public int? ConcentrationCurrent { get; set; }
    public int? ConcentrationMaximum { get; set; }
    public long? ConcentrationScanTime { get; set; }

    public int ReadyChargesNow { get; set; }
    public int? NextChargeRemainingSeconds { get; set; }
    public int? ConcentrationSimulated { get; set; }

    public string GetNotificationKey()
    {
        var recipe = RecipeId?.ToString() ?? "0";
        return string.Join("|", CharacterKey, Profession, ItemName, recipe);
    }

    public string GetCharacterDisplayName()
    {
        return string.IsNullOrWhiteSpace(RealmName)
            ? CharacterName
            : $"{CharacterName}-{RealmName}";
    }
}

internal sealed class DesktopCompanionSettings
{
    public bool CharactersInitialized { get; set; }
    public bool ProfessionsInitialized { get; set; }
    public bool ExpansionsInitialized { get; set; }
    public bool ItemsInitialized { get; set; }
    public string SavedVariablesFilePath { get; set; } = string.Empty;
    public bool DarkMode { get; set; } = true;
    public string SortColumnName { get; set; } = "Ready";
    public bool SortAscending { get; set; } = true;
    public HashSet<string> SelectedCharacters { get; set; } = [];
    public HashSet<string> SelectedProfessions { get; set; } = [];
    public HashSet<string> SelectedExpansions { get; set; } = [];
    public HashSet<string> SelectedItems { get; set; } = [];
    public Dictionary<string, bool> NotificationEnabledByKey { get; set; } = new(StringComparer.Ordinal);

    public bool IsNotificationEnabled(CooldownRecord cooldown)
    {
        return !NotificationEnabledByKey.TryGetValue(cooldown.GetNotificationKey(), out var enabled) || enabled;
    }

    public void SetNotificationEnabled(CooldownRecord cooldown, bool enabled)
    {
        NotificationEnabledByKey[cooldown.GetNotificationKey()] = enabled;
    }
}
