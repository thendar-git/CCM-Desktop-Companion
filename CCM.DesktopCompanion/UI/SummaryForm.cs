using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.UI;

internal sealed class SummaryForm : Form
{
    private readonly Label _summaryLabel;
    private readonly Button _gearButton;
    private readonly CheckedListBox _characterFilter;
    private readonly CheckedListBox _professionFilter;
    private readonly CheckedListBox _expansionFilter;
    private readonly CheckedListBox _itemFilter;
    private readonly DataGridView _cooldownGrid;
    private readonly ToolTip _toolTip = new();

    private List<CooldownRecord> _visibleCooldowns = [];
    private bool _isUpdatingFilters;
    private string _sortColumnName = "Ready";
    private bool _sortAscending = true;
    private bool _isDarkMode = true;
    private string _currentSavedVariablesPath = string.Empty;
    private Dictionary<string, string> _characterClassByKey = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? FiltersChanged;
    public event Action<CooldownRecord, bool>? NotificationEnabledChanged;
    public event Action<string, bool>? SortChanged;
    public event Action<string>? SavedVariablesPathChanged;
    public event Action<bool>? DarkModeChanged;

    public SummaryForm(bool darkMode = true)
    {
        _isDarkMode = darkMode;

        Text = "CCM Desktop Companion";
        Width = 1195;
        Height = 640;
        MinimumSize = new Size(1195, 640);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Top bar: summary label + gear icon ───────────────────────────
        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12, 6, 8, 6),
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Waiting for CCM desktop snapshot...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
        };

        _gearButton = new Button
        {
            Text = "\u2699",
            Width = 34,
            Height = 30,
            Anchor = AnchorStyles.Right,
            Font = new Font("Segoe UI Symbol", 14f),
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true,
            Padding = new Padding(0),
            Margin = new Padding(0, 1, 4, 1),
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        _gearButton.Click += (_, _) => OpenSettings();
        _toolTip.SetToolTip(_gearButton, "Settings");

        topBar.Controls.Add(_summaryLabel, 0, 0);
        topBar.Controls.Add(_gearButton, 1, 0);

        // ── Gold separator ────────────────────────────────────────────────
        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 2,
            Tag = "separator",
        };
        separator.Paint += (_, e) =>
        {
            using var pen = new Pen(WowTheme.GoldDark, 1);
            e.Graphics.DrawLine(pen, 0, 0, separator.Width, 0);
            using var pen2 = new Pen(WowTheme.GoldMid, 1);
            e.Graphics.DrawLine(pen2, 0, 1, separator.Width, 1);
        };

        // ── Filter panel ──────────────────────────────────────────────────
        var filterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 175,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(10, 6, 10, 6),
        };
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        _characterFilter = BuildFilterList();
        _professionFilter = BuildFilterList();
        _expansionFilter = BuildFilterList();
        _itemFilter = BuildFilterList();

        filterPanel.Controls.Add(BuildFilterGroup("Characters", _characterFilter), 0, 0);
        filterPanel.Controls.Add(BuildFilterGroup("Professions", _professionFilter), 1, 0);
        filterPanel.Controls.Add(BuildFilterGroup("Expansions", _expansionFilter), 2, 0);
        filterPanel.Controls.Add(BuildFilterGroup("Cooldown Items", _itemFilter), 3, 0);

        // ── Cooldown grid ─────────────────────────────────────────────────
        _cooldownGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            MultiSelect = false,
            ReadOnly = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        EnsureGridColumns();
        _cooldownGrid.CellValueChanged += CooldownGridOnCellValueChanged;
        _cooldownGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_cooldownGrid.IsCurrentCellDirty)
            {
                _cooldownGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _cooldownGrid.ColumnHeaderMouseClick += CooldownGridOnColumnHeaderMouseClick;
        _cooldownGrid.CellFormatting += CooldownGridOnCellFormatting;

        Controls.Add(_cooldownGrid);
        Controls.Add(filterPanel);
        Controls.Add(separator);
        Controls.Add(topBar);

        ApplyTheme(_isDarkMode);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void SetFilterOptions(IReadOnlyList<string> characters, IReadOnlyList<string> professions, IReadOnlyList<string> expansions, IReadOnlyList<string> items, DesktopCompanionSettings settings)
    {
        _sortColumnName = settings.SortColumnName;
        _sortAscending = settings.SortAscending;

        _isUpdatingFilters = true;
        try
        {
            ApplyFilterItems(_characterFilter, characters, settings.SelectedCharacters);
            ApplyFilterItems(_professionFilter, professions, settings.SelectedProfessions);
            ApplyFilterItems(_expansionFilter, expansions, settings.SelectedExpansions);
            ApplyFilterItems(_itemFilter, items, settings.SelectedItems);
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    public void UpdateSnapshot(DesktopSnapshot snapshot, IReadOnlyList<CooldownRecord> visibleCooldowns, DesktopCompanionSettings settings)
    {
        // Rebuild class lookup
        _characterClassByKey = snapshot.Characters
            .Where(c => !string.IsNullOrWhiteSpace(c.Class))
            .ToDictionary(c => c.CharacterKey, c => c.Class, StringComparer.OrdinalIgnoreCase);

        EnsureGridColumns();
        _visibleCooldowns = SortCooldowns(visibleCooldowns).ToList();
        _cooldownGrid.SuspendLayout();
        _cooldownGrid.Rows.Clear();

        var generatedText = snapshot.GeneratedAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(snapshot.GeneratedAt).ToLocalTime().ToString("g")
            : "n/a";
        _summaryLabel.Text = $"Snapshot v{snapshot.SchemaVersion}  \u2502  Generated: {generatedText}  \u2502  Visible: {_visibleCooldowns.Count}  \u2502  Ready: {snapshot.GetReadyCooldownCount(_visibleCooldowns)}";

        foreach (var cooldown in _visibleCooldowns)
        {
            _cooldownGrid.Rows.Add(
                settings.IsNotificationEnabled(cooldown),
                cooldown.GetCharacterDisplayName(),
                cooldown.Profession,
                string.IsNullOrWhiteSpace(cooldown.Expansion) ? "Unknown" : cooldown.Expansion,
                cooldown.ItemName,
                FormatReady(cooldown),
                FormatCharges(cooldown),
                FormatNextCharge(cooldown),
                FormatConcentration(cooldown));
            _cooldownGrid.Rows[^1].Tag = cooldown;
        }

        UpdateSortGlyphs();
        _cooldownGrid.ResumeLayout();
    }

    public void SetSavedVariablesPath(string path)
    {
        _currentSavedVariablesPath = path;
    }

    public void SetTheme(bool darkMode)
    {
        if (_isDarkMode == darkMode) return;
        _isDarkMode = darkMode;
        ApplyTheme(darkMode);
    }

    public HashSet<string> GetSelectedCharacters() => GetSelectedValues(_characterFilter);
    public HashSet<string> GetSelectedProfessions() => GetSelectedValues(_professionFilter);
    public HashSet<string> GetSelectedExpansions() => GetSelectedValues(_expansionFilter);
    public HashSet<string> GetSelectedItems() => GetSelectedValues(_itemFilter);

    // ── Theme ─────────────────────────────────────────────────────────────

    private void ApplyTheme(bool dark)
    {
        BackColor = WowTheme.Background(dark);
        ForeColor = WowTheme.Text(dark);
        WowTheme.StyleGearButton(_gearButton, dark);
        _summaryLabel.ForeColor = dark ? WowTheme.GoldBright : WowTheme.LightText;
        _summaryLabel.BackColor = Color.Transparent;
        WowTheme.ApplyToForm(this, dark);
        WowTheme.ApplyToDataGridView(_cooldownGrid, dark);
        _cooldownGrid.Invalidate();
    }

    // ── Settings dialog ───────────────────────────────────────────────────

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_currentSavedVariablesPath, _isDarkMode)
        {
            Icon = Icon,
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (!string.Equals(dlg.SavedVariablesPath, _currentSavedVariablesPath, StringComparison.Ordinal))
        {
            _currentSavedVariablesPath = dlg.SavedVariablesPath;
            SavedVariablesPathChanged?.Invoke(_currentSavedVariablesPath);
        }

        if (dlg.DarkMode != _isDarkMode)
        {
            _isDarkMode = dlg.DarkMode;
            ApplyTheme(_isDarkMode);
            DarkModeChanged?.Invoke(_isDarkMode);
        }
    }

    // ── Grid setup ────────────────────────────────────────────────────────

    private void EnsureGridColumns()
    {
        if (_cooldownGrid.Columns.Count > 0) return;

        _cooldownGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Notify",
            HeaderText = "Notify",
            Width = 55,
            DataPropertyName = "Notify",
            SortMode = DataGridViewColumnSortMode.Programmatic,
        });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Character",     HeaderText = "Character",     Width = 160, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profession",    HeaderText = "Profession",    Width = 120, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expansion",     HeaderText = "Expansion",     Width = 150, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName",      HeaderText = "Cooldown Item", Width = 320, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ready",         HeaderText = "READY",         Width = 60,  ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Charges",       HeaderText = "Charges",       Width = 70,  ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NextCharge",    HeaderText = "Next Charge",   Width = 110, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Concentration", HeaderText = "Concentration", Width = 110, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
    }

    private void CooldownGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;

        // Character column — WoW class color
        if (e.ColumnIndex == 1 && e.CellStyle != null)
        {
            if (_cooldownGrid.Rows[e.RowIndex].Tag is CooldownRecord cooldown)
            {
                _characterClassByKey.TryGetValue(cooldown.CharacterKey, out var cls);
                e.CellStyle.ForeColor = WowTheme.GetCharacterColor(cooldown.CharacterKey, cls, _isDarkMode);
                e.CellStyle.Font = new Font(_cooldownGrid.Font!, FontStyle.Bold);
            }
            return;
        }

        // READY column — green / red
        if (e.ColumnIndex == 5 && e.CellStyle != null)
        {
            var val = e.Value?.ToString();
            if (val == "YES")
            {
                e.CellStyle.ForeColor = WowTheme.ReadyYes;
                e.CellStyle.Font = new Font(_cooldownGrid.Font!, FontStyle.Bold);
            }
            else if (val == "NO")
            {
                e.CellStyle.ForeColor = WowTheme.ReadyNo;
            }
        }
    }

    // ── Filter helpers ────────────────────────────────────────────────────

    private CheckedListBox BuildFilterList()
    {
        return new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
            ScrollAlwaysVisible = true,
        };
    }

    private GroupBox BuildFilterGroup(string title, CheckedListBox list)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var allButton  = new Button { Text = "All",  Width = 52, Height = 24, Margin = new Padding(0, 2, 4, 2) };
        var noneButton = new Button { Text = "None", Width = 52, Height = 24, Margin = new Padding(0, 2, 0, 2) };
        allButton.Click  += (_, _) => SetAllItems(list, true);
        noneButton.Click += (_, _) => SetAllItems(list, false);

        buttonPanel.Controls.Add(allButton);
        buttonPanel.Controls.Add(noneButton);
        group.Controls.Add(list);
        group.Controls.Add(buttonPanel);

        list.ItemCheck += (_, _) =>
        {
            if (_isUpdatingFilters) return;
            BeginInvoke(new Action(() => FiltersChanged?.Invoke(this, EventArgs.Empty)));
        };

        return group;
    }

    private void ApplyFilterItems(CheckedListBox list, IReadOnlyList<string> values, HashSet<string> selectedValues)
    {
        list.Items.Clear();
        foreach (var value in values)
        {
            list.Items.Add(value, selectedValues.Contains(value));
        }
    }

    private void SetAllItems(CheckedListBox list, bool isChecked)
    {
        _isUpdatingFilters = true;
        try
        {
            for (var i = 0; i < list.Items.Count; i++)
                list.SetItemChecked(i, isChecked);
        }
        finally
        {
            _isUpdatingFilters = false;
        }
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HashSet<string> GetSelectedValues(CheckedListBox list)
    {
        return list.CheckedItems.Cast<object>()
            .Select(item => item.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    // ── Grid events ───────────────────────────────────────────────────────

    private void CooldownGridOnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
        if (_cooldownGrid.Rows[e.RowIndex].Tag is CooldownRecord cooldown)
        {
            var enabled = Convert.ToBoolean(_cooldownGrid.Rows[e.RowIndex].Cells[0].Value ?? false);
            NotificationEnabledChanged?.Invoke(cooldown, enabled);
        }
    }

    private void CooldownGridOnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var column = _cooldownGrid.Columns[e.ColumnIndex];
        if (column == null) return;

        if (string.Equals(_sortColumnName, column.Name, StringComparison.Ordinal))
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumnName = column.Name;
            _sortAscending = true;
        }

        SortChanged?.Invoke(_sortColumnName, _sortAscending);
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Sort ──────────────────────────────────────────────────────────────

    private IEnumerable<CooldownRecord> SortCooldowns(IReadOnlyList<CooldownRecord> visibleCooldowns)
    {
        if (string.Equals(_sortColumnName, "Ready", StringComparison.Ordinal))
        {
            return _sortAscending
                ? visibleCooldowns
                    .OrderByDescending(c => c.ReadyChargesNow > 0)
                    .ThenBy(c => c.NextChargeRemainingSeconds ?? int.MaxValue)
                    .ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase)
                : visibleCooldowns
                    .OrderBy(c => c.ReadyChargesNow > 0)
                    .ThenByDescending(c => c.NextChargeRemainingSeconds ?? int.MinValue)
                    .ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase);
        }

        Func<CooldownRecord, object?> keySelector = _sortColumnName switch
        {
            "Notify"        => c => c.Enabled,
            "Character"     => c => c.GetCharacterDisplayName(),
            "Profession"    => c => c.Profession,
            "Expansion"     => c => string.IsNullOrWhiteSpace(c.Expansion) ? "Unknown" : c.Expansion,
            "ItemName"      => c => c.ItemName,
            "Charges"       => c => (c.MaxCharges.HasValue && c.MaxCharges.Value > 0)
                                        ? (double)c.ReadyChargesNow / c.MaxCharges.Value
                                        : c.ReadyChargesNow,
            "NextCharge"    => c => c.NextChargeRemainingSeconds ?? int.MaxValue,
            "Concentration" => c => c.ConcentrationSimulated ?? -1,
            _               => c => c.GetCharacterDisplayName(),
        };

        return _sortAscending
            ? visibleCooldowns.OrderBy(keySelector).ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase)
            : visibleCooldowns.OrderByDescending(keySelector).ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _cooldownGrid.Columns)
            column.HeaderCell.SortGlyphDirection = SortOrder.None;

        if (_cooldownGrid.Columns.Contains(_sortColumnName))
            _cooldownGrid.Columns[_sortColumnName].HeaderCell.SortGlyphDirection = _sortAscending ? SortOrder.Ascending : SortOrder.Descending;
    }

    // ── Formatters ────────────────────────────────────────────────────────

    private static string FormatCharges(CooldownRecord c)
    {
        var max = c.MaxCharges;
        return max.HasValue && max.Value > 0
            ? $"{c.ReadyChargesNow} / {max.Value}"
            : c.ReadyChargesNow.ToString();
    }

    private static string FormatConcentration(CooldownRecord c)
    {
        if (c.ConcentrationSimulated == null || c.ConcentrationMaximum == null)
            return string.Empty;
        return $"{c.ConcentrationSimulated} / {c.ConcentrationMaximum}";
    }

    private static string FormatReady(CooldownRecord c)
        => c.ReadyChargesNow > 0 ? "YES" : "NO";

    private static string FormatNextCharge(CooldownRecord c)
    {
        if (c.NextChargeRemainingSeconds == null)
            return c.ReadyChargesNow > 0 ? "\u2014" : "n/a";
        return FormatDuration(c.NextChargeRemainingSeconds.Value);
    }

    private static string FormatDuration(int totalSeconds)
    {
        var remaining = Math.Max(0, totalSeconds);
        var hours = remaining / 3600;
        var minutes = (remaining % 3600) / 60;
        return hours <= 0 ? $"{minutes}m" : $"{hours}h {minutes}m";
    }
}
