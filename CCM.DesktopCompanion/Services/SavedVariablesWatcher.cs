using System.Timers;

namespace CCM.DesktopCompanion.Services;

internal sealed class SavedVariablesWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounceTimer;

    public event EventHandler? SavedVariablesChanged;

    public SavedVariablesWatcher(string filePath)
    {
        _debounceTimer = new System.Timers.Timer(500)
        {
            AutoReset = false,
        };
        _debounceTimer.Elapsed += (_, _) => SavedVariablesChanged?.Invoke(this, EventArgs.Empty);

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(fileName) && Directory.Exists(directory))
        {
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Renamed += OnChanged;
        }
    }

    public void Start()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    public void Dispose()
    {
        _debounceTimer.Dispose();
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }

    private void OnChanged(object? sender, FileSystemEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }
}
