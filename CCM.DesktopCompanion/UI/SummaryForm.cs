using System.Drawing;
using System.Windows.Forms;
using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.UI;

internal sealed class SummaryForm : Form
{
    private readonly Label _summaryLabel;
    private readonly CheckedListBox _characterFilter;
    private readonly CheckedListBox _professionFilter;
    private readonly CheckedListBox _expansionFilter;
    private readonly DataGridView _cooldownGrid;
    private List<CooldownRecord> _visibleCooldowns = [];
    private bool _isUpdatingFilters;
    private string _sortColumnName = "Ready";
    private bool _sortAscending = true;

    public event EventHandler? FiltersChanged;
    public event Action<CooldownRecord, bool>? NotificationEnabledChanged;
    public event Action<string, bool>? SortChanged;

    public SummaryForm()
    {
        Text = "CCM Desktop Companion";
        Width = 1040;
        Height = 660;
        StartPosition = FormStartPosition.CenterScreen;

        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Waiting for CCM desktop snapshot...",
        };

        var filterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 180,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(12, 8, 12, 8),
        };
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        filterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _characterFilter = BuildFilterList();
        _professionFilter = BuildFilterList();
        _expansionFilter = BuildFilterList();

        filterPanel.Controls.Add(BuildFilterGroup("Characters", _characterFilter), 0, 0);
        filterPanel.Controls.Add(BuildFilterGroup("Professions", _professionFilter), 1, 0);
        filterPanel.Controls.Add(BuildFilterGroup("Expansions", _expansionFilter), 2, 0);
        filterPanel.SetRowSpan(filterPanel.Controls[0], 2);
        filterPanel.SetRowSpan(filterPanel.Controls[1], 2);
        filterPanel.SetRowSpan(filterPanel.Controls[2], 2);

        _cooldownGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
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

        Controls.Add(_cooldownGrid);
        Controls.Add(filterPanel);
        Controls.Add(_summaryLabel);
    }

    public void SetFilterOptions(IReadOnlyList<string> characters, IReadOnlyList<string> professions, IReadOnlyList<string> expansions, DesktopCompanionSettings settings)
    {
        _sortColumnName = settings.SortColumnName;
        _sortAscending = settings.SortAscending;

        _isUpdatingFilters = true;
        try
        {
            ApplyFilterItems(_characterFilter, characters, settings.SelectedCharacters);
            ApplyFilterItems(_professionFilter, professions, settings.SelectedProfessions);
            ApplyFilterItems(_expansionFilter, expansions, settings.SelectedExpansions);
        }
        finally
        {
            _isUpdatingFilters = false;
        }
    }

    public void UpdateSnapshot(DesktopSnapshot snapshot, IReadOnlyList<CooldownRecord> visibleCooldowns, DesktopCompanionSettings settings)
    {
        EnsureGridColumns();
        _visibleCooldowns = SortCooldowns(visibleCooldowns).ToList();
        _cooldownGrid.SuspendLayout();
        _cooldownGrid.Rows.Clear();

        var generatedText = snapshot.GeneratedAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(snapshot.GeneratedAt).ToLocalTime().ToString("g")
            : "n/a";
        _summaryLabel.Text = $"Snapshot v{snapshot.SchemaVersion} | Generated: {generatedText} | Visible: {_visibleCooldowns.Count} | Ready: {snapshot.GetReadyCooldownCount(_visibleCooldowns)}";

        foreach (var cooldown in _visibleCooldowns)
        {
            _cooldownGrid.Rows.Add(
                settings.IsNotificationEnabled(cooldown),
                cooldown.GetCharacterDisplayName(),
                cooldown.Profession,
                string.IsNullOrWhiteSpace(cooldown.Expansion) ? "Unknown" : cooldown.Expansion,
                cooldown.ItemName,
                FormatReady(cooldown),
                cooldown.ReadyChargesNow.ToString(),
                FormatNextCharge(cooldown));
            _cooldownGrid.Rows[^1].Tag = cooldown;
        }

        UpdateSortGlyphs();
        _cooldownGrid.ResumeLayout();
    }

    public HashSet<string> GetSelectedCharacters() => GetSelectedValues(_characterFilter);
    public HashSet<string> GetSelectedProfessions() => GetSelectedValues(_professionFilter);
    public HashSet<string> GetSelectedExpansions() => GetSelectedValues(_expansionFilter);

    private static CheckedListBox BuildFilterList()
    {
        return new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };
    }

    private void EnsureGridColumns()
    {
        if (_cooldownGrid.Columns.Count > 0)
        {
            return;
        }

        _cooldownGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Notify",
            HeaderText = "Notify",
            Width = 55,
            DataPropertyName = "Notify",
            SortMode = DataGridViewColumnSortMode.Programmatic,
        });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Character", HeaderText = "Character", Width = 160, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profession", HeaderText = "Profession", Width = 120, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expansion", HeaderText = "Expansion", Width = 150, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Cooldown Item", Width = 320, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ready", HeaderText = "READY", Width = 60, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Charges", HeaderText = "Charges", Width = 70, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
        _cooldownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NextCharge", HeaderText = "Next Charge", Width = 110, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic });
    }

    private GroupBox BuildFilterGroup(string title, CheckedListBox list)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var allButton = new Button { Text = "All", Width = 54, Height = 24 };
        allButton.Click += (_, _) => SetAllItems(list, true);

        var noneButton = new Button { Text = "None", Width = 54, Height = 24 };
        noneButton.Click += (_, _) => SetAllItems(list, false);

        buttonPanel.Controls.Add(allButton);
        buttonPanel.Controls.Add(noneButton);
        group.Controls.Add(list);
        group.Controls.Add(buttonPanel);

        list.ItemCheck += (_, _) =>
        {
            if (_isUpdatingFilters)
            {
                return;
            }

            BeginInvoke(new Action(() => FiltersChanged?.Invoke(this, EventArgs.Empty)));
        };

        return group;
    }

    private void CooldownGridOnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0)
        {
            return;
        }

        if (_cooldownGrid.Rows[e.RowIndex].Tag is CooldownRecord cooldown)
        {
            var enabled = Convert.ToBoolean(_cooldownGrid.Rows[e.RowIndex].Cells[0].Value ?? false);
            NotificationEnabledChanged?.Invoke(cooldown, enabled);
        }
    }

    private void CooldownGridOnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var column = _cooldownGrid.Columns[e.ColumnIndex];
        if (column == null)
        {
            return;
        }

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
            {
                list.SetItemChecked(i, isChecked);
            }
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

    private IEnumerable<CooldownRecord> SortCooldowns(IReadOnlyList<CooldownRecord> visibleCooldowns)
    {
        if (string.Equals(_sortColumnName, "Ready", StringComparison.Ordinal))
        {
            return _sortAscending
                ? visibleCooldowns
                    .OrderByDescending(cooldown => cooldown.ReadyChargesNow > 0)
                    .ThenBy(cooldown => cooldown.NextChargeRemainingSeconds ?? int.MaxValue)
                    .ThenBy(cooldown => cooldown.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase)
                : visibleCooldowns
                    .OrderBy(cooldown => cooldown.ReadyChargesNow > 0)
                    .ThenByDescending(cooldown => cooldown.NextChargeRemainingSeconds ?? int.MinValue)
                    .ThenBy(cooldown => cooldown.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase);
        }

        Func<CooldownRecord, object?> keySelector = _sortColumnName switch
        {
            "Notify" => cooldown => cooldown.Enabled,
            "Character" => cooldown => cooldown.GetCharacterDisplayName(),
            "Profession" => cooldown => cooldown.Profession,
            "Expansion" => cooldown => string.IsNullOrWhiteSpace(cooldown.Expansion) ? "Unknown" : cooldown.Expansion,
            "ItemName" => cooldown => cooldown.ItemName,
            "Charges" => cooldown => cooldown.ReadyChargesNow,
            "NextCharge" => cooldown => cooldown.NextChargeRemainingSeconds ?? int.MaxValue,
            _ => cooldown => cooldown.GetCharacterDisplayName(),
        };

        return _sortAscending
            ? visibleCooldowns.OrderBy(keySelector).ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase)
            : visibleCooldowns.OrderByDescending(keySelector).ThenBy(c => c.GetCharacterDisplayName(), StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateSortGlyphs()
    {
        foreach (DataGridViewColumn column in _cooldownGrid.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        if (_cooldownGrid.Columns.Contains(_sortColumnName))
        {
            _cooldownGrid.Columns[_sortColumnName].HeaderCell.SortGlyphDirection = _sortAscending ? SortOrder.Ascending : SortOrder.Descending;
        }
    }

    private static string FormatReady(CooldownRecord cooldown)
    {
        return cooldown.ReadyChargesNow > 0 ? "YES" : "NO";
    }

    private static string FormatNextCharge(CooldownRecord cooldown)
    {
        if (cooldown.NextChargeRemainingSeconds == null)
        {
            return "n/a";
        }

        return FormatDuration(cooldown.NextChargeRemainingSeconds.Value);
    }

    private static string FormatDuration(int totalSeconds)
    {
        var remaining = Math.Max(0, totalSeconds);
        var hours = remaining / 3600;
        var minutes = (remaining % 3600) / 60;
        if (hours <= 0)
        {
            return $"{minutes}m";
        }

        return $"{hours}h {minutes}m";
    }
}