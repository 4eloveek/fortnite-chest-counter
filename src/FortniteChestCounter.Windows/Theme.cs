namespace FortniteChestCounter.Windows;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(20, 24, 33);
    public static readonly Color Card = Color.FromArgb(31, 37, 49);
    public static readonly Color Foreground = Color.FromArgb(237, 241, 248);
    public static readonly Color Muted = Color.FromArgb(159, 172, 193);
    public static readonly Color Accent = Color.FromArgb(100, 225, 188);

    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Foreground,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Margin = new Padding(0, 4, 0, 5)
    };

    public static Button Button(string text, bool accent = false) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 5, 10, 5),
        FlatStyle = FlatStyle.Flat,
        BackColor = accent ? Accent : Card,
        ForeColor = accent ? Background : Foreground,
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 4, 8, 4)
    };

    public static ComboBox ComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = Card,
        ForeColor = Foreground,
        FlatStyle = FlatStyle.Flat,
        Width = 250,
        Margin = new Padding(8, 4, 16, 4)
    };

    public static CheckBox CheckBox(string text, bool value) => new()
    {
        Text = text,
        Checked = value,
        AutoSize = true,
        ForeColor = Foreground,
        Margin = new Padding(0, 5, 12, 5)
    };
}
