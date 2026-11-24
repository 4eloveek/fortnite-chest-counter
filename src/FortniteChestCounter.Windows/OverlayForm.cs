using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal sealed class OverlayForm : Form
{
    private readonly Font _titleFont = new("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _countFont = new("Segoe UI", 44, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _detailFont = new("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
    private CounterSnapshot _counts;
    private string _status = "Пауза";

    public OverlayForm()
    {
        Text = "Chest counter overlay";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(242, 147);
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Theme.Background;
        Opacity = 0.92;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020; // No activation, tool window, click-through
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ExcludeFromCapture(Handle);
    }

    public void UpdateCounts(CounterSnapshot counts, string status)
    {
        if (_counts == counts && _status == status)
        {
            return;
        }

        _counts = counts;
        _status = status;
        Invalidate();
    }

    public void Place(Screen screen, OverlayCorner corner)
    {
        Rectangle bounds = screen.WorkingArea;
        int margin = 22;
        int left = Math.Max(bounds.Left, bounds.Left + margin);
        int right = Math.Max(bounds.Left, bounds.Right - Width - margin);
        int top = Math.Max(bounds.Top, bounds.Top + margin);
        int bottom = Math.Max(bounds.Top, bounds.Bottom - Height - margin);
        Location = corner switch
        {
            OverlayCorner.TopRight => new Point(right, top),
            OverlayCorner.BottomLeft => new Point(left, bottom),
            OverlayCorner.BottomRight => new Point(right, bottom),
            _ => new Point(left, top)
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Scale custom drawing to the monitor DPI as well as the form itself.
        float scale = DeviceDpi / 96f;
        e.Graphics.ScaleTransform(scale, scale);
        using var muted = new SolidBrush(Theme.Muted);
        using var accent = new SolidBrush(Theme.Accent);
        e.Graphics.DrawString("СУНДУКИ · ТЕКУЩИЙ МАТЧ", _titleFont, muted, 14, 10);
        e.Graphics.DrawString(_counts.Session.ToString(), _countFont, accent, 12, 29);
        e.Graphics.DrawString($"Сегодня: {_counts.Today}    Всего: {_counts.Total}", _detailFont, Brushes.White, 14, 91);
        e.Graphics.DrawString(_status, _detailFont, muted, 14, 118);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0084) // WM_NCHITTEST
        {
            m.Result = new IntPtr(-1); // HTTRANSPARENT
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _countFont.Dispose();
            _detailFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
