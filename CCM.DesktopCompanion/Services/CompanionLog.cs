namespace CCM.DesktopCompanion.Services;

internal static class CompanionLog
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CCM.DesktopCompanion");
    private static readonly string LogPathValue = Path.Combine(LogDirectory, "CCM.DesktopCompanion.log");

    public static string LogPath => LogPathValue;

    public static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPathValue,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break the desktop companion.
        }
    }
}
