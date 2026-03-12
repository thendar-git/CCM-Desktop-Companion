using System.Drawing;
using System.Windows.Forms;
using CommunityToolkit.WinUI.Notifications;
using CCM.DesktopCompanion.Models;
using CCM.DesktopCompanion.Services;
using CCM.DesktopCompanion.UI;

namespace CCM.DesktopCompanion;

internal sealed class CompanionApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private SummaryForm? _summaryForm;
    private readonly DesktopSnapshotReader _reader;
    private readonly SavedVariablesWatcher _watcher;
    private readonly RuntimeStateCalculator _runtimeStateCalculator;
    private readonly NotificationService _notificationService;
    private readonly SnapshotFilterService _snapshotFilterService;
    private readonly DesktopSettingsService _settingsService;
    private readonly System.Windows.Forms.Timer _runtimeTimer;
    private DesktopSnapshot _currentSnapshot = DesktopSnapshot.Empty;
    private DesktopCompanionSettings _settings;

    public CompanionApplicationContext()
    {
        CompanionLog.Write("Companion application context starting.");
        ToastNotificationManagerCompat.OnActivated += args =>
        {
            CompanionLog.Write($"Toast activated. Arguments={args.Argument}");
        };

        _reader = new DesktopSnapshotReader();
        _runtimeStateCalculator = new RuntimeStateCalculator();
        _notificationService = new NotificationService();
        _snapshotFilterService = new SnapshotFilterService();
        _settingsService = new DesktopSettingsService();
        _settings = _settingsService.Load();

        EnsureSummaryForm();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (_, _) => ShowSummary());
        contextMenu.Items.Add("Refresh", null, (_, _) => RefreshSnapshot());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Text = "CCM",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = contextMenu,
        };

        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                if (_summaryForm is { IsDisposed: false, Visible: true })
                {
                    _summaryForm.Hide();
                }
                else
                {
                    ShowSummary();
                }
            }
        };

        var savedVariablesPath = _reader.FindDefaultSavedVariablesPath();
        CompanionLog.Write(string.IsNullOrWhiteSpace(savedVariablesPath)
            ? "No CCM.lua SavedVariables file was found."
            : $"Watching SavedVariables file: {savedVariablesPath}");

        _watcher = new SavedVariablesWatcher(savedVariablesPath);
        _watcher.SavedVariablesChanged += (_, _) => RefreshSnapshot();
        _watcher.Start();

        _runtimeTimer = new System.Windows.Forms.Timer
        {
            Interval = 15000,
        };
        _runtimeTimer.Tick += (_, _) => ApplyRuntimeStateAndRefreshUi();
        _runtimeTimer.Start();

        RefreshSnapshot();
    }

    protected override void ExitThreadCore()
    {
        CompanionLog.Write("Companion application context shutting down.");
        PersistFilters();
        _watcher.Dispose();
        _runtimeTimer.Stop();
        _runtimeTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_summaryForm is { IsDisposed: false })
        {
            _summaryForm.Dispose();
        }
        base.ExitThreadCore();
    }

    private void EnsureSummaryForm()
    {
        if (_summaryForm is { IsDisposed: false })
        {
            return;
        }

        var form = new SummaryForm();
        form.FiltersChanged += (_, _) =>
        {
            PersistFilters();
            ApplyRuntimeStateAndRefreshUi();
        };
        form.NotificationEnabledChanged += (cooldown, enabled) =>
        {
            _settings.SetNotificationEnabled(cooldown, enabled);
            _settingsService.Save(_settings);
            ApplyRuntimeStateAndRefreshUi();
        };
        form.SortChanged += (columnName, ascending) =>
        {
            _settings.SortColumnName = columnName;
            _settings.SortAscending = ascending;
            _settingsService.Save(_settings);
        };
        form.FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                form.Hide();
            }
        };

        _summaryForm = form;
    }

    private void ShowSummary()
    {
        EnsureSummaryForm();
        RefreshSnapshot();
        _summaryForm!.Show();
        _summaryForm.BringToFront();
        _summaryForm.Activate();
    }

    private void RefreshSnapshot()
    {
        if (_reader.TryReadSnapshot(out var snapshot))
        {
            _currentSnapshot = snapshot;
            CompanionLog.Write($"Loaded snapshot. Schema={snapshot.SchemaVersion}; Source={snapshot.SourceFile}; Characters={snapshot.Characters.Count}; Cooldowns={snapshot.Cooldowns.Count}");
            ApplyRuntimeStateAndRefreshUi();
        }
        else
        {
            _currentSnapshot = DesktopSnapshot.Empty;
            CompanionLog.Write("Snapshot read returned no data.");
            ApplyRuntimeStateAndRefreshUi();
        }
    }

    private void ApplyRuntimeStateAndRefreshUi()
    {
        EnsureSummaryForm();

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _runtimeStateCalculator.ApplyRuntimeState(_currentSnapshot, nowUnix);

        var characters = _snapshotFilterService.GetCharacterOptions(_currentSnapshot);
        var professions = _snapshotFilterService.GetProfessionOptions(_currentSnapshot);
        var expansions = _snapshotFilterService.GetExpansionOptions(_currentSnapshot);
        EnsureInitializedSelections(characters, professions, expansions);
        NormalizeSelectedValues(_settings.SelectedCharacters, characters);
        NormalizeSelectedValues(_settings.SelectedProfessions, professions);
        NormalizeSelectedValues(_settings.SelectedExpansions, expansions);

        _summaryForm!.SetFilterOptions(characters, professions, expansions, _settings);
        var filteredCooldowns = _snapshotFilterService.ApplyFilters(_currentSnapshot, _settings);
        _summaryForm.UpdateSnapshot(_currentSnapshot, filteredCooldowns, _settings);

        _notifyIcon.Text = filteredCooldowns.Count > 0
            ? $"CCM: {_currentSnapshot.GetReadyCooldownCount(filteredCooldowns.Where(_settings.IsNotificationEnabled))} ready"
            : "CCM";
        _notificationService.ProcessSnapshot(filteredCooldowns.Where(_settings.IsNotificationEnabled));
    }

    private void PersistFilters()
    {
        if (_summaryForm is null || _summaryForm.IsDisposed)
        {
            return;
        }

        _settings.SelectedCharacters = _summaryForm.GetSelectedCharacters();
        _settings.SelectedProfessions = _summaryForm.GetSelectedProfessions();
        _settings.SelectedExpansions = _summaryForm.GetSelectedExpansions();
        _settings.CharactersInitialized = true;
        _settings.ProfessionsInitialized = true;
        _settings.ExpansionsInitialized = true;
        _settingsService.Save(_settings);
    }

    private void EnsureInitializedSelections(IReadOnlyList<string> characters, IReadOnlyList<string> professions, IReadOnlyList<string> expansions)
    {
        var hasChanges = false;

        if (!_settings.CharactersInitialized)
        {
            _settings.SelectedCharacters = characters.ToHashSet(StringComparer.Ordinal);
            _settings.CharactersInitialized = true;
            hasChanges = true;
        }
        if (!_settings.ProfessionsInitialized)
        {
            _settings.SelectedProfessions = professions.ToHashSet(StringComparer.Ordinal);
            _settings.ProfessionsInitialized = true;
            hasChanges = true;
        }
        if (!_settings.ExpansionsInitialized)
        {
            _settings.SelectedExpansions = expansions.ToHashSet(StringComparer.Ordinal);
            _settings.ExpansionsInitialized = true;
            hasChanges = true;
        }

        if (hasChanges)
        {
            _settingsService.Save(_settings);
        }
    }

    private static void NormalizeSelectedValues(HashSet<string> selectedValues, IReadOnlyList<string> validValues)
    {
        selectedValues.RemoveWhere(value => !validValues.Contains(value, StringComparer.OrdinalIgnoreCase));
    }
}
