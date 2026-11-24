using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal sealed class StatisticsForm : Form
{
    private enum Period { Last7Days, Last30Days, ThisMonth, AllTime, Custom }

    private readonly CounterLedger _ledger;
    private readonly ComboBox _period = Theme.ComboBox();
    private readonly DateTimePicker _from = DatePicker("Начало периода");
    private readonly DateTimePicker _through = DatePicker("Конец периода");
    private readonly Label _summary = Theme.Label("", 12, true);
    private readonly DailyChartControl _chart = new();
    private bool _updating;
    private int _displayedTotal = -1;
    private DateOnly _displayedToday;

    public StatisticsForm(CounterLedger ledger)
    {
        _ledger = ledger;
        Text = "Статистика по дням";
        Font = new Font("Segoe UI", 10);
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(930, 560);
        MinimumSize = new Size(720, 420);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++)
        {
            root.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
        }

        Controls.Add(root);
        root.Controls.Add(Theme.Label("Сундуки по дням", 21, true), 0, 0);
        Label subtitle = Theme.Label("Каждый столбец — один день. Учитываются все матчи и ручные поправки.");
        subtitle.ForeColor = Theme.Muted;
        subtitle.Dock = DockStyle.Fill;
        root.Controls.Add(subtitle, 0, 1);

        _period.Width = 165;
        _period.AccessibleName = "Период статистики";
        _period.Items.AddRange(["7 дней", "30 дней", "Этот месяц", "Всё время", "Свой период"]);
        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = new Padding(0, 8, 0, 8)
        };
        filters.Controls.AddRange([Theme.Label("Период:"), _period, Theme.Label("С:"), _from,
            Theme.Label("По:"), _through]);
        root.Controls.Add(filters, 0, 2);
        _summary.Dock = DockStyle.Fill;
        _summary.Margin = new Padding(0, 4, 0, 12);
        root.Controls.Add(_summary, 0, 3);
        _chart.Margin = Padding.Empty;
        root.Controls.Add(_chart, 0, 4);
        Label help = Theme.Label("Наведите курсор на столбец для точного числа. Длинный период прокручивается по горизонтали.");
        help.ForeColor = Theme.Muted;
        help.Dock = DockStyle.Fill;
        help.Margin = new Padding(0, 10, 0, 0);
        root.Controls.Add(help, 0, 5);

        _period.SelectedIndexChanged += (_, _) => ApplyPeriod(resetScroll: true);
        _from.ValueChanged += (_, _) => { if (!_updating) UpdateChart(resetScroll: true); };
        _through.ValueChanged += (_, _) => { if (!_updating) UpdateChart(resetScroll: true); };
        _period.SelectedIndex = (int)Period.Last7Days;
    }

    private static DateTimePicker DatePicker(string accessibleName) => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy",
        Value = DateTime.Today, MaxDate = DateTime.Today, Width = 120,
        Margin = new Padding(8, 4, 16, 4), AccessibleName = accessibleName,
        CalendarMonthBackground = Theme.Card, CalendarForeColor = Theme.Foreground
    };

    public void RefreshCounts(int total)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        if (_displayedTotal == total && _displayedToday == today) return;
        ApplyPeriod(resetScroll: false);
    }

    private void ApplyPeriod(bool resetScroll)
    {
        DateTime today = DateTime.Today;
        Period period = (Period)_period.SelectedIndex;
        _updating = true;
        try
        {
            _from.MaxDate = today;
            _through.MaxDate = today;
            bool custom = period == Period.Custom;
            _from.Enabled = custom;
            _through.Enabled = custom;
            if (!custom)
            {
                DateTime from = period switch
                {
                    Period.Last7Days => today.AddDays(-6),
                    Period.Last30Days => today.AddDays(-29),
                    Period.ThisMonth => new DateTime(today.Year, today.Month, 1),
                    _ => (_ledger.FirstRecordedDay ?? DateOnly.FromDateTime(today)).ToDateTime(TimeOnly.MinValue)
                };
                _from.Value = from < _from.MinDate ? _from.MinDate : from > today ? today : from;
                _through.Value = today;
            }
        }
        finally
        {
            _updating = false;
        }

        UpdateChart(resetScroll);
    }

    private void UpdateChart(bool resetScroll)
    {
        DateOnly from = DateOnly.FromDateTime(_from.Value);
        DateOnly through = DateOnly.FromDateTime(_through.Value);
        _displayedToday = DateOnly.FromDateTime(DateTime.Today);
        _displayedTotal = _ledger.Snapshot(DateTimeOffset.Now).Total;
        if (through < from)
        {
            _summary.Text = "Дата «с» должна быть не позже даты «по».";
            _summary.ForeColor = Color.FromArgb(255, 194, 120);
            _chart.SetDays([], resetScroll, "Выберите корректный период.");
            return;
        }

        IReadOnlyList<DailyChestCount> days = _ledger.DailyCounts(from, through);
        int total = days.Sum(day => day.Count);
        int activeDays = days.Count(day => day.Count > 0);
        _summary.Text = $"{from:dd.MM.yyyy} — {through:dd.MM.yyyy}  ·  Открыто: {total}  ·  Дней с открытиями: {activeDays} из {days.Count}";
        _summary.ForeColor = Theme.Foreground;
        _chart.SetDays(days, resetScroll);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ExcludeFromCapture(Handle);
    }
}
