using System.Text.Json;
using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.Services;

internal sealed class DesktopSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public DesktopSettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CCM.DesktopCompanion");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public DesktopCompanionSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new DesktopCompanionSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<DesktopCompanionSettings>(json, JsonOptions) ?? new DesktopCompanionSettings();

            if (string.IsNullOrWhiteSpace(settings.SortColumnName)
                || (string.Equals(settings.SortColumnName, "Character", StringComparison.Ordinal) && settings.SortAscending))
            {
                settings.SortColumnName = "Ready";
                settings.SortAscending = true;
            }

            return settings;
        }
        catch (Exception ex)
        {
            CompanionLog.Write($"Settings load failure: {ex.GetType().Name}: {ex.Message}");
            return new DesktopCompanionSettings();
        }
    }

    public void Save(DesktopCompanionSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            CompanionLog.Write($"Settings save failure: {ex.GetType().Name}: {ex.Message}");
        }
    }
}