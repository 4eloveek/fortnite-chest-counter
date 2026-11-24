using System.Drawing.Drawing2D;
using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal sealed class RegionSelectorForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly Rectangle _monitorBounds;
    private Point? _start;
    private Rectangle _selection;

    public CaptureArea? SelectedArea { get; private set; }

    public RegionSelectorForm(Rectangle monitorBounds, Bitmap screenshot)
    {
        _monitorBounds = monitorBounds;
        _screenshot = screenshot;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        Bounds = monitorBounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_screenshot, Point.Empty);
        using var dim = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        e.Graphics.FillRectangle(dim, ClientRectangle);

        if (_selection.Width > 0 && _selection.Height > 0)
        {
            e.Graphics.DrawImage(_screenshot, _selection, _selection, GraphicsUnit.Pixel);
            using var border = new Pen(Theme.Accent, 2) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawRectangle(border, _selection);
        }

        using var panel = new SolidBrush(Theme.Background);
        e.Graphics.FillRectangle(panel, 20, 20, Math.Min(790, ClientSize.Width - 40), 92);
        using var title = new Font("Segoe UI", 14, FontStyle.Bold);
        using var text = new Font("Segoe UI", 10);
        e.Graphics.DrawString("Выделите уведомление о сундуке вместе с начислением XP", title, Brushes.White, 34, 31);
        e.Graphics.DrawString("Кадр заморожен. Потяните мышью • Esc / правая кнопка — отмена", text, Brushes.White, 34, 66);
        e.Graphics.DrawString($"Область: {_selection.Width} × {_selection.Height} пикселей", text, Brushes.White, 34, 86);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        else if (e.Button == MouseButtons.Left)
        {
            _start = e.Location;
            _selection = Rectangle.Empty;
            Capture = true;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_start is not { } start)
        {
            return;
        }

        int x = Math.Clamp(e.X, 0, ClientSize.Width);
        int y = Math.Clamp(e.Y, 0, ClientSize.Height);
        _selection = Rectangle.FromLTRB(Math.Min(start.X, x), Math.Min(start.Y, y),
            Math.Max(start.X, x), Math.Max(start.Y, y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _start is not null)
        {
            Capture = false;
            _start = null;
            CompleteSelection();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        else if (e.KeyCode == Keys.Enter)
        {
            CompleteSelection();
        }
    }

    private void CompleteSelection()
    {
        if (_selection.Width < 50 || _selection.Height < 20)
        {
            return;
        }

        SelectedArea = new CaptureArea(_monitorBounds.X + _selection.X,
            _monitorBounds.Y + _selection.Y, _selection.Width, _selection.Height);
        DialogResult = DialogResult.OK;
        Close();
    }
}
