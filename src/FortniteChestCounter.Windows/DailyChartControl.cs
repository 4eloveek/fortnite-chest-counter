using System.Drawing.Drawing2D;
using System.Globalization;
using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal sealed class DailyChartControl : ScrollableControl
{
    private readonly ToolTip _tooltip = new() { InitialDelay = 150, ReshowDelay = 50, AutoPopDelay = 10000 };
    private IReadOnlyList<DailyChestCount> _days = [];
    private int _maximum;
    private int _hoveredIndex = -1;
    private string _emptyMessage = "В выбранном периоде открытий нет.";

    public DailyChartControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        ForeColor = Theme.Foreground;
        Font = new Font("Segoe UI", 9);
        AutoScroll = true;
        Dock = DockStyle.Fill;
        AccessibleName = "Столбчатый график открытых сундуков по дням";
        AccessibleRole = AccessibleRole.Graphic;
    }

    public void SetDays(IReadOnlyList<DailyChestCount> days, bool resetScroll, string? emptyMessage = null)
    {
        _days = days;
        _maximum = days.Count == 0 ? 0 : days.Max(day => day.Count);
        _emptyMessage = emptyMessage ?? "В выбранном периоде открытий нет.";
        AccessibleDescription = $"Дней: {days.Count}. Открыто сундуков: {days.Sum(day => day.Count)}.";
        ClearHover();
        UpdateScrollExtent();
        if (resetScroll) AutoScrollPosition = Point.Empty;
        Invalidate();
    }

    private int Pixels(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));

    private void UpdateScrollExtent()
    {
        int minimumWidth = _days.Count == 0 ? 0 : Pixels(54) + Pixels(18) + _days.Count * Pixels(48);
        var size = new Size(minimumWidth > ClientSize.Width ? minimumWidth : 0, 0);
        if (AutoScrollMinSize != size) AutoScrollMinSize = size;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollExtent();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateScrollExtent();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float left = Pixels(54), right = Pixels(18), top = Pixels(28), bottom = Pixels(58);
        float width = ClientSize.Width - left - right;
        float height = ClientSize.Height - top - bottom;
        if (width <= 0 || height <= 0) return;

        using var textBrush = new SolidBrush(Theme.Foreground);
        using var mutedBrush = new SolidBrush(Theme.Muted);
        using var barBrush = new SolidBrush(Theme.Accent);
        using var hoverBrush = new SolidBrush(Color.FromArgb(155, 242, 215));
        using var gridPen = new Pen(Color.FromArgb(45, Theme.Muted));
        using var zeroPen = new Pen(Color.FromArgb(110, Theme.Muted), Pixels(2));
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var tickFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        double step = TickStep(Math.Max(1, _maximum));
        double axisMaximum = Math.Ceiling(Math.Max(1, _maximum) / step) * step;
        for (double tick = 0; tick <= axisMaximum; tick += step)
        {
            float y = top + height - (float)(tick / axisMaximum * height);
            graphics.DrawLine(gridPen, left, y, left + width, y);
            graphics.DrawString(tick.ToString("0", CultureInfo.CurrentCulture), Font, mutedBrush,
                new RectangleF(0, y - Font.Height, left - Pixels(9), Font.Height * 2), tickFormat);
        }

        if (_days.Count > 0)
        {
            double slot = Math.Max(width / _days.Count, Pixels(48));
            int offset = AutoScrollPosition.X;
            int first = Math.Max(0, (int)Math.Floor(-offset / slot) - 1);
            int last = Math.Min(_days.Count - 1, (int)Math.Ceiling((width - offset) / slot));
            GraphicsState state = graphics.Save();
            graphics.SetClip(new RectangleF(left, 0, width, ClientSize.Height));
            for (int i = first; i <= last; i++)
            {
                DailyChestCount day = _days[i];
                float center = left + offset + (float)((i + 0.5) * slot);
                float barWidth = Math.Min(Pixels(68), (float)slot * 0.65f);
                float barHeight = (float)(day.Count / axisMaximum * height);
                float baseline = top + height;
                if (day.Count > 0)
                {
                    graphics.FillRectangle(i == _hoveredIndex ? hoverBrush : barBrush,
                        center - barWidth / 2, baseline - barHeight, barWidth, barHeight);
                }
                else
                {
                    graphics.DrawLine(zeroPen, center - barWidth / 2, baseline, center + barWidth / 2, baseline);
                }

                graphics.DrawString(day.Count.ToString(CultureInfo.CurrentCulture), Font, textBrush,
                    new RectangleF(center - (float)slot / 2, baseline - barHeight - Font.Height - Pixels(5),
                        (float)slot, Font.Height), centered);
                graphics.DrawString(day.Day.ToString("dd.MM", CultureInfo.InvariantCulture), Font, mutedBrush,
                    new RectangleF(center - (float)slot / 2, baseline + Pixels(9), (float)slot, Font.Height), centered);
                graphics.DrawString(day.Day.ToString("yyyy", CultureInfo.InvariantCulture), Font, mutedBrush,
                    new RectangleF(center - (float)slot / 2, baseline + Pixels(9) + Font.Height, (float)slot, Font.Height), centered);
            }

            graphics.Restore(state);
        }

        if (_maximum == 0)
        {
            graphics.DrawString(_emptyMessage, Font, mutedBrush,
                new RectangleF(left, top, width, height), centered);
        }
    }

    private static double TickStep(int maximum)
    {
        double rough = Math.Max(1, maximum / 4d);
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double normalized = rough / magnitude;
        return (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * magnitude;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int left = Pixels(54), right = Pixels(18);
        int width = ClientSize.Width - left - right;
        int index = -1;
        if (_days.Count > 0 && width > 0 && e.X >= left && e.X < left + width)
        {
            double slot = Math.Max(width / (double)_days.Count, Pixels(48));
            index = (int)((e.X - left - AutoScrollPosition.X) / slot);
            if (index >= _days.Count) index = -1;
        }

        if (index == _hoveredIndex) return;
        _hoveredIndex = index;
        _tooltip.SetToolTip(this, index < 0 ? null :
            $"{_days[index].Day:dd.MM.yyyy}\nОткрыто сундуков: {_days[index].Count}");
        Invalidate();
    }

    private void ClearHover()
    {
        _hoveredIndex = -1;
        _tooltip.SetToolTip(this, null);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
        Invalidate();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        ClearHover();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!HorizontalScroll.Visible)
        {
            base.OnMouseWheel(e);
            return;
        }

        AutoScrollPosition = new Point(Math.Max(0, -AutoScrollPosition.X - e.Delta / 120 * Pixels(48) * 3), 0);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        ClearHover();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tooltip.Dispose();
        base.Dispose(disposing);
    }
}
