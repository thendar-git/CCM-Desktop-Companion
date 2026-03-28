using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CCM.DesktopCompanion.UI;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _pathText;
    private readonly RadioButton _darkRadio;
    private readonly RadioButton _lightRadio;

    public string SavedVariablesPath { get; private set; }
    public bool DarkMode { get; private set; }

    public SettingsForm(string currentPath, bool darkMode)
    {
        SavedVariablesPath = currentPath;
        DarkMode = darkMode;

        Text = "CCM Settings";
        Width = 620;
        Height = 230;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // path label
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // path row
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // theme row
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // buttons

        var pathLabel = new Label
        {
            Text = "SavedVariables file (CCM.lua):",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4),
        };

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _pathText = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = string.IsNullOrWhiteSpace(currentPath) ? "Browse to CCM.lua" : currentPath,
            Margin = new Padding(0, 0, 8, 0),
        };

        var browseButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Text = "Browse...",
            Margin = new Padding(0, 2, 0, 2),
            Anchor = AnchorStyles.Right,
        };
        browseButton.Click += (_, _) => BrowsePath();

        pathRow.Controls.Add(_pathText, 0, 0);
        pathRow.Controls.Add(browseButton, 1, 0);

        var themeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var themeLabel = new Label
        {
            Text = "Theme:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0),
        };

        _darkRadio = new RadioButton
        {
            Text = "Dark",
            AutoSize = true,
            Checked = darkMode,
            Margin = new Padding(0, 6, 16, 0),
        };
        _lightRadio = new RadioButton
        {
            Text = "Light",
            AutoSize = true,
            Checked = !darkMode,
            Margin = new Padding(0, 6, 0, 0),
        };

        themeRow.Controls.Add(themeLabel);
        themeRow.Controls.Add(_darkRadio);
        themeRow.Controls.Add(_lightRadio);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 80,
            Margin = new Padding(8, 0, 0, 0),
        };
        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Width = 80,
        };
        okButton.Click += (_, _) =>
        {
            var path = _pathText.Text;
            SavedVariablesPath = string.Equals(path, "Browse to CCM.lua", StringComparison.Ordinal) ? string.Empty : path;
            DarkMode = _darkRadio.Checked;
            DialogResult = DialogResult.OK;
            Close();
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        buttonRow.Controls.Add(cancelButton);
        buttonRow.Controls.Add(okButton);

        layout.Controls.Add(pathLabel, 0, 0);
        layout.Controls.Add(pathRow, 0, 1);
        layout.Controls.Add(themeRow, 0, 2);
        layout.Controls.Add(buttonRow, 0, 3);

        Controls.Add(layout);

        ApplyTheme(darkMode);
    }

    private void ApplyTheme(bool dark)
    {
        BackColor = WowTheme.Background(dark);
        ForeColor = WowTheme.Text(dark);
        WowTheme.ApplyToForm(this, dark);
    }

    private void BrowsePath()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select CCM.lua SavedVariables file",
            Filter = "CCM SavedVariables (CCM.lua)|CCM.lua|Lua files (*.lua)|*.lua|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            FileName = "CCM.lua",
        };

        var current = _pathText.Text;
        if (!string.IsNullOrWhiteSpace(current) && !string.Equals(current, "Browse to CCM.lua", StringComparison.Ordinal))
        {
            try
            {
                var dir = Path.GetDirectoryName(current);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                }
                var file = Path.GetFileName(current);
                if (!string.IsNullOrWhiteSpace(file))
                {
                    dialog.FileName = file;
                }
            }
            catch { /* ignore malformed path */ }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathText.Text = dialog.FileName;
        }
    }
}
