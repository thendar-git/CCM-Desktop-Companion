using System.Drawing;
using System.Windows.Forms;

namespace CCM.DesktopCompanion.UI;

internal static class WowTheme
{
    // Dark palette
    public static readonly Color DarkBackground   = Color.FromArgb(0x1A, 0x1A, 0x26);
    public static readonly Color DarkPanel        = Color.FromArgb(0x10, 0x10, 0x1C);
    public static readonly Color DarkHeader       = Color.FromArgb(0x22, 0x22, 0x36);
    public static readonly Color DarkGridBg       = Color.FromArgb(0x14, 0x14, 0x20);
    public static readonly Color DarkGridAlt      = Color.FromArgb(0x1C, 0x1C, 0x2C);
    public static readonly Color DarkGridHeader   = Color.FromArgb(0x26, 0x26, 0x3A);
    public static readonly Color DarkGridLine     = Color.FromArgb(0x30, 0x28, 0x14);
    public static readonly Color DarkText         = Color.FromArgb(0xE0, 0xCE, 0x9A);
    public static readonly Color DarkSubText      = Color.FromArgb(0x90, 0x80, 0x58);
    public static readonly Color DarkSelection    = Color.FromArgb(0x38, 0x34, 0x18);
    public static readonly Color DarkSelectionFg  = Color.FromArgb(0xFF, 0xEE, 0x80);

    // Light palette
    public static readonly Color LightBackground  = SystemColors.Control;
    public static readonly Color LightPanel       = SystemColors.Window;
    public static readonly Color LightHeader      = SystemColors.Control;
    public static readonly Color LightGridBg      = SystemColors.Window;
    public static readonly Color LightGridAlt     = Color.FromArgb(0xF4, 0xF0, 0xE8);
    public static readonly Color LightGridHeader  = SystemColors.Control;
    public static readonly Color LightGridLine    = SystemColors.ControlLight;
    public static readonly Color LightText        = SystemColors.ControlText;
    public static readonly Color LightSubText     = SystemColors.GrayText;
    public static readonly Color LightSelection   = SystemColors.Highlight;
    public static readonly Color LightSelectionFg = SystemColors.HighlightText;

    // WoW accent
    public static readonly Color GoldBright  = Color.FromArgb(0xFF, 0xD7, 0x00);
    public static readonly Color GoldMid     = Color.FromArgb(0xC8, 0xA0, 0x30);
    public static readonly Color GoldDark    = Color.FromArgb(0x6A, 0x56, 0x20);
    public static readonly Color ReadyYes    = Color.FromArgb(0x1E, 0xFF, 0x00);
    public static readonly Color ReadyNo     = Color.FromArgb(0xFF, 0x44, 0x44);

    // WoW class colors
    private static readonly Dictionary<string, Color> ClassColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DeathKnight"]  = Color.FromArgb(0xC4, 0x1E, 0x3A),
        ["Death Knight"] = Color.FromArgb(0xC4, 0x1E, 0x3A),
        ["DemonHunter"]  = Color.FromArgb(0xA3, 0x30, 0xC9),
        ["Demon Hunter"] = Color.FromArgb(0xA3, 0x30, 0xC9),
        ["Druid"]        = Color.FromArgb(0xFF, 0x7C, 0x0A),
        ["Evoker"]       = Color.FromArgb(0x33, 0x93, 0x7F),
        ["Hunter"]       = Color.FromArgb(0xAA, 0xD3, 0x72),
        ["Mage"]         = Color.FromArgb(0x3F, 0xC7, 0xEB),
        ["Monk"]         = Color.FromArgb(0x00, 0xFF, 0x98),
        ["Paladin"]      = Color.FromArgb(0xF4, 0x8C, 0xBA),
        ["Priest"]       = Color.FromArgb(0xF0, 0xEB, 0xE0),
        ["Rogue"]        = Color.FromArgb(0xFF, 0xF4, 0x68),
        ["Shaman"]       = Color.FromArgb(0x00, 0x70, 0xDD),
        ["Warlock"]      = Color.FromArgb(0x87, 0x88, 0xEE),
        ["Warrior"]      = Color.FromArgb(0xC6, 0x9B, 0x3A),
    };

    // Stable ordered palette used for deterministic fallback assignment
    private static readonly Color[] ClassColorPalette =
    [
        Color.FromArgb(0xC4, 0x1E, 0x3A), // Death Knight
        Color.FromArgb(0xA3, 0x30, 0xC9), // Demon Hunter
        Color.FromArgb(0xFF, 0x7C, 0x0A), // Druid
        Color.FromArgb(0x33, 0x93, 0x7F), // Evoker
        Color.FromArgb(0xAA, 0xD3, 0x72), // Hunter
        Color.FromArgb(0x3F, 0xC7, 0xEB), // Mage
        Color.FromArgb(0x00, 0xFF, 0x98), // Monk
        Color.FromArgb(0xF4, 0x8C, 0xBA), // Paladin
        Color.FromArgb(0xF0, 0xEB, 0xE0), // Priest
        Color.FromArgb(0xFF, 0xF4, 0x68), // Rogue
        Color.FromArgb(0x00, 0x70, 0xDD), // Shaman
        Color.FromArgb(0x87, 0x88, 0xEE), // Warlock
        Color.FromArgb(0xC6, 0x9B, 0x3A), // Warrior
    ];

    /// <summary>
    /// Returns a WoW class color for the character. If the class name is known it maps
    /// directly; otherwise a stable color is derived from the character key so every
    /// character still gets a distinct, consistent WoW-palette color.
    /// </summary>
    public static Color GetCharacterColor(string characterKey, string? className, bool dark)
    {
        if (!string.IsNullOrWhiteSpace(className) && ClassColors.TryGetValue(className, out var classColor))
        {
            return classColor;
        }

        // Deterministic fallback: hash the character key to a palette slot
        var hash = 0;
        foreach (var ch in characterKey)
        {
            hash = hash * 31 + char.ToUpperInvariant(ch);
        }
        return ClassColorPalette[Math.Abs(hash) % ClassColorPalette.Length];
    }

    public static Color Background(bool dark) => dark ? DarkBackground  : LightBackground;
    public static Color Panel(bool dark)      => dark ? DarkPanel       : LightPanel;
    public static Color Header(bool dark)     => dark ? DarkHeader      : LightHeader;
    public static Color GridBg(bool dark)     => dark ? DarkGridBg      : LightGridBg;
    public static Color GridAlt(bool dark)    => dark ? DarkGridAlt     : LightGridAlt;
    public static Color GridHeader(bool dark) => dark ? DarkGridHeader  : LightGridHeader;
    public static Color GridLine(bool dark)   => dark ? DarkGridLine    : LightGridLine;
    public static Color Text(bool dark)       => dark ? DarkText        : LightText;
    public static Color SubText(bool dark)    => dark ? DarkSubText     : LightSubText;
    public static Color Selection(bool dark)  => dark ? DarkSelection   : LightSelection;
    public static Color SelectionFg(bool dark)=> dark ? DarkSelectionFg : LightSelectionFg;

    public static void StyleButton(Button btn, bool dark)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = dark ? DarkHeader : LightHeader;
        btn.ForeColor = dark ? GoldBright : LightText;
        btn.FlatAppearance.BorderColor = dark ? GoldDark : SystemColors.ControlDark;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(0x38, 0x34, 0x18) : SystemColors.ControlLight;
    }

    public static void StyleGearButton(Button btn, bool dark)
    {
        StyleButton(btn, dark);
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Color.Transparent;
        btn.ForeColor = dark ? GoldMid : SystemColors.ControlDarkDark;
        btn.FlatAppearance.MouseOverBackColor = dark
            ? Color.FromArgb(0x40, 0x38, 0x18)
            : SystemColors.ControlLight;
    }

    public static void ApplyToDataGridView(DataGridView dgv, bool dark)
    {
        dgv.EnableHeadersVisualStyles = false;
        dgv.BackgroundColor = GridBg(dark);
        dgv.GridColor = GridLine(dark);

        dgv.DefaultCellStyle.BackColor         = GridBg(dark);
        dgv.DefaultCellStyle.ForeColor         = Text(dark);
        dgv.DefaultCellStyle.SelectionBackColor= Selection(dark);
        dgv.DefaultCellStyle.SelectionForeColor= SelectionFg(dark);

        dgv.AlternatingRowsDefaultCellStyle.BackColor          = GridAlt(dark);
        dgv.AlternatingRowsDefaultCellStyle.ForeColor          = Text(dark);
        dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Selection(dark);
        dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = SelectionFg(dark);

        dgv.ColumnHeadersDefaultCellStyle.BackColor         = GridHeader(dark);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor         = dark ? GoldBright : LightText;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor= GridHeader(dark);
        dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor= dark ? GoldBright : LightText;
        dgv.ColumnHeadersDefaultCellStyle.Font              = new Font(dgv.Font, FontStyle.Bold);
    }

    public static void ApplyToForm(Control root, bool dark)
    {
        ApplyToControlTree(root, dark);
    }

    private static void ApplyToControlTree(Control control, bool dark)
    {
        switch (control)
        {
            case DataGridView dgv:
                ApplyToDataGridView(dgv, dark);
                return;
            case CheckedListBox clb:
                clb.BackColor = Panel(dark);
                clb.ForeColor = Text(dark);
                return;
            case Button btn:
                StyleButton(btn, dark);
                return;
            case GroupBox gb:
                gb.BackColor = Background(dark);
                gb.ForeColor = dark ? GoldBright : LightText;
                break;
            case Label lbl:
                lbl.BackColor = Color.Transparent;
                lbl.ForeColor = Text(dark);
                break;
            case TextBox tb:
                tb.BackColor = Panel(dark);
                tb.ForeColor = Text(dark);
                tb.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                break;
            case TableLayoutPanel tlp:
                tlp.BackColor = Background(dark);
                break;
            case FlowLayoutPanel flp:
                flp.BackColor = Background(dark);
                break;
            case Panel p:
                // skip separator panels — they manage their own color
                if (p.Tag is "separator") return;
                p.BackColor = Background(dark);
                break;
            default:
                control.BackColor = Background(dark);
                control.ForeColor = Text(dark);
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControlTree(child, dark);
        }
    }
}
