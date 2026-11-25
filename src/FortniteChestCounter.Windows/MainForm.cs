using System.Diagnostics;
using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal sealed class MainForm : Form
{
    private sealed record MonitorChoice(Screen Screen)
    {
        public override string ToString() => $"{Screen.DeviceName} · {Screen.Bounds.Width} × {Screen.Bounds.Height}";
    }

    private sealed record Choice<T>(string Title, T Value)
    {
        public override string ToString() => Title;
    }

    private const int AddChestHotkeyId = 9;

    private sealed record ScanReport(
        IReadOnlyList<OcrLine>? Lines, ChestNotification? Notification, bool Visible, TimeSpan Time,
        int DurationMs, bool Waiting = false, string? Error = null);

    private AppSettings _settings;
    private readonly string _settingsPath;
    private readonly CounterLedger _ledger;
    private readonly NotificationGate _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly OverlayForm _overlay = new();
    private readonly System.Windows.Forms.Timer _statsTimer = new() { Interval = 1000 };
    private readonly List<int> _registeredHotkeys = [];
    private readonly List<Control> _configurationControls = [];
    private readonly HotkeyCaptureButton _addKeyButton = new();

    private readonly Label _session = Theme.Label("0", 34, true);
    private readonly Label _today = Theme.Label("0", 28, true);
    private readonly Label _total = Theme.Label("0", 28, true);
    private readonly Label _status = Theme.Label("Пауза", 11, true);
    private readonly Label _lastCount = Theme.Label("После открытия сундука здесь появится время подсчёта.");
    private readonly Label _areaLabel = Theme.Label("");
    private readonly Label _hotkeyInfo = Theme.Label("");
    private readonly Button _startButton = Theme.Button("Начать", accent: true);
    private readonly Button _selectButton = Theme.Button("Выбрать область · Ctrl+Alt+F7");
    private readonly Button _overlayButton = Theme.Button("Скрыть счётчик");
    private readonly ComboBox _monitors = Theme.ComboBox();
    private readonly ComboBox _languages = Theme.ComboBox();
    private readonly ComboBox _corners = Theme.ComboBox();
    private readonly TextBox _recognizedText = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, Height = 82, BackColor = Theme.Card, ForeColor = Theme.Foreground,
        Font = new Font("Consolas", 10), BorderStyle = BorderStyle.FixedSingle,
        Text = "Распознанный текст появится после запуска и перехода в Fortnite."
    };

    private CancellationTokenSource? _cancellation;
    private Task? _scanTask;
    private StatisticsForm? _statistics;
    private long _runId;
    private bool _running;
    private bool _transitioning;
    private bool _selecting;
    private bool _closing;
    private bool _allowClose;
    private bool _loading = true;
    private bool _addHotkeyRegistered;
    private string _overlayStatus = "Пауза";

    public MainForm(AppSettings settings, string settingsPath, CounterLedger ledger)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _ledger = ledger;
        Text = "Счётчик сундуков";
        Font = new Font("Segoe UI", 10);
        BackColor = Theme.Background;
        ForeColor = Theme.Foreground;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(806, 780);
        MinimumSize = new Size(700, 560);
        BuildContent();
        _loading = false;
        RefreshAreaLabel();
        UpdateCounts();

        Theme.Style(_addKeyButton);
        _addKeyButton.Click += (_, _) => _addKeyButton.BeginCapture();
        _addKeyButton.Captured += ApplyAddChestHotkey;
        _addKeyButton.Cancelled += UpdateAddHotkeyButton;
        UpdateAddHotkeyButton();

        _startButton.Click += async (_, _) => await ToggleRunningAsync();
        _selectButton.Click += async (_, _) => await SelectAreaAsync();
        _overlayButton.Click += (_, _) => ToggleOverlay();
        _statsTimer.Tick += (_, _) => UpdateCounts();
        Shown += (_, _) => { ApplyOverlay(); _statsTimer.Start(); };
        FormClosing += OnClosing;
    }

    private void BuildContent()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        void Add(Control control)
        {
            int row = root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Top;
            root.Controls.Add(control, 0, row);
        }

        Add(Theme.Label("Fortnite · счётчик сундуков", 21, true));
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "0.1";
        var subtitle = Theme.Label($"Автоматический подсчёт по уведомлениям XP на экране · прототип {version}");
        subtitle.ForeColor = Theme.Muted;
        Add(subtitle);

        var stats = new TableLayoutPanel
        {
            ColumnCount = 3, Height = 116, BackColor = Theme.Card,
            Margin = new Padding(0, 12, 0, 9), Padding = new Padding(12)
        };
        for (int i = 0; i < 3; i++)
        {
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        }

        stats.Controls.Add(StatsCell("Текущий матч", _session), 0, 0);
        stats.Controls.Add(StatsCell("Сегодня", _today), 1, 0);
        stats.Controls.Add(StatsCell("Всего", _total), 2, 0);
        _session.ForeColor = Theme.Accent;
        Add(stats);
        Add(_status);
        _lastCount.ForeColor = Theme.Muted;
        Add(_lastCount);

        Button plus = Theme.Button("+1");
        Button minus = Theme.Button("−1");
        Button newMatch = Theme.Button("Новый матч");
        Button statistics = Theme.Button("Статистика по дням");
        plus.Click += async (_, _) => await ChangeCountAsync(() =>
        {
            _ledger.Add(DateTimeOffset.Now, CountSource.Manual);
            _gate.SuppressUntilClear();
            _lastCount.Text = $"Последний сундук: {DateTime.Now:HH:mm:ss} · добавлен вручную";
        });
        minus.Click += async (_, _) => await UndoAsync();
        newMatch.Click += async (_, _) => await NewMatchAsync();
        statistics.Click += (_, _) => ShowStatistics();
        Add(Flow(_startButton, plus, minus, newMatch, _overlayButton, statistics));

        Add(Theme.Label("Область распознавания", 12, true));
        foreach (Screen screen in Screen.AllScreens)
        {
            _monitors.Items.Add(new MonitorChoice(screen));
        }

        _monitors.SelectedIndex = Enumerable.Range(0, _monitors.Items.Count)
            .FirstOrDefault(i => ((MonitorChoice)_monitors.Items[i]!).Screen.DeviceName == _settings.MonitorDeviceName,
                Enumerable.Range(0, _monitors.Items.Count)
                    .FirstOrDefault(i => ((MonitorChoice)_monitors.Items[i]!).Screen.Primary));
        _monitors.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                SaveSettings(_settings with { MonitorDeviceName = SelectedScreen.DeviceName, Area = null });
                RefreshAreaLabel();
                ApplyOverlay();
            }
        };
        _configurationControls.Add(_monitors);
        _configurationControls.Add(_selectButton);
        Add(Flow(Theme.Label("Монитор:"), _monitors, _selectButton));
        _areaLabel.ForeColor = Theme.Muted;
        Add(_areaLabel);
        Add(Theme.Label("В игре: откройте сундук → нажмите Ctrl+Alt+F7, пока видно XP → выделите XP и название."));

        _languages.Items.AddRange([
            new Choice<string>("Русский + English", "rus+eng"),
            new Choice<string>("Русский", "rus"),
            new Choice<string>("English", "eng")
        ]);
        _languages.Width = 175;
        _languages.SelectedIndex = Enumerable.Range(0, _languages.Items.Count)
            .First(i => ((Choice<string>)_languages.Items[i]!).Value == _settings.Language);
        _languages.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                SaveSettings(_settings with { Language = ((Choice<string>)_languages.SelectedItem!).Value });
            }
        };
        _configurationControls.Add(_languages);

        var interval = new NumericUpDown
        {
            Minimum = 100, Maximum = 2000, Increment = 50, Value = _settings.ScanIntervalMs,
            Width = 82, BackColor = Theme.Card, ForeColor = Theme.Foreground,
            Margin = new Padding(8, 4, 4, 4)
        };
        interval.ValueChanged += (_, _) =>
        {
            if (!_loading)
            {
                SaveSettings(_settings with { ScanIntervalMs = (int)interval.Value });
            }
        };
        _configurationControls.Add(interval);
        Add(Flow(Theme.Label("Язык:"), _languages, Theme.Label("Интервал:"), interval, Theme.Label("мс")));

        var requireXp = Theme.CheckBox("Проверять начисление XP рядом со словом «сундук»", _settings.RequireXp);
        var brightMask = Theme.CheckBox("Фильтр яркого текста: белый / жёлтый / фиолетовый", _settings.BrightTextMask);
        var foreground = Theme.CheckBox("Распознавать только когда активно окно Fortnite", _settings.OnlyWhenFortniteActive);
        requireXp.CheckedChanged += (_, _) => { if (!_loading) SaveSettings(_settings with { RequireXp = requireXp.Checked }); };
        brightMask.CheckedChanged += (_, _) => { if (!_loading) SaveSettings(_settings with { BrightTextMask = brightMask.Checked }); };
        foreground.CheckedChanged += (_, _) => { if (!_loading) SaveSettings(_settings with { OnlyWhenFortniteActive = foreground.Checked }); };
        _configurationControls.AddRange([requireXp, brightMask, foreground]);
        Add(requireXp);
        Add(brightMask);
        Add(foreground);

        _corners.Items.AddRange([
            new Choice<OverlayCorner>("Слева сверху", OverlayCorner.TopLeft),
            new Choice<OverlayCorner>("Справа сверху", OverlayCorner.TopRight),
            new Choice<OverlayCorner>("Слева снизу", OverlayCorner.BottomLeft),
            new Choice<OverlayCorner>("Справа снизу", OverlayCorner.BottomRight)
        ]);
        _corners.Width = 180;
        _corners.SelectedIndex = Enumerable.Range(0, _corners.Items.Count)
            .First(i => ((Choice<OverlayCorner>)_corners.Items[i]!).Value == _settings.OverlayCorner);
        _corners.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                SaveSettings(_settings with { OverlayCorner = ((Choice<OverlayCorner>)_corners.SelectedItem!).Value });
                ApplyOverlay();
            }
        };
        Add(Flow(Theme.Label("Положение счётчика:"), _corners));
        Add(Theme.Label("Диагностика распознавания", 11, true));
        Add(_recognizedText);
        var keys = Theme.Label("Ctrl+Alt: F8 — старт/пауза · F9 — +1 (по умолчанию) · F10 — −1 · F11 — новый матч · F12 — счётчик");
        keys.ForeColor = Theme.Muted;
        Add(keys);
        Add(Flow(Theme.Label("Клавиша «+1»:"), _addKeyButton));
        _hotkeyInfo.ForeColor = Theme.Muted;
        Add(_hotkeyInfo);
        var footer = Theme.Label("Матч сбрасывается кнопкой или F11. Статистика сохраняется автоматически.");
        footer.ForeColor = Theme.Muted;
        Add(footer);
    }

    private static Control StatsCell(string title, Label number)
    {
        var cell = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true
        };
        Label heading = Theme.Label(title);
        heading.ForeColor = Theme.Muted;
        cell.Controls.Add(heading);
        cell.Controls.Add(number);
        return cell;
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = new Padding(0, 3, 0, 3)
        };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private Screen SelectedScreen => ((MonitorChoice)_monitors.SelectedItem!).Screen;

    private void RefreshAreaLabel()
    {
        _areaLabel.Text = _settings.Area is { } area
            ? $"Выбрано: {area.Width} × {area.Height} px · X={area.X}, Y={area.Y}"
            : "Область ещё не выбрана. Выделите небольшую область с уведомлением о сундуке.";
    }

    private bool SaveSettings(AppSettings next)
    {
        try
        {
            next.Validate();
            JsonFile.Write(_settingsPath, next);
            _settings = next;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось сохранить настройки.\n\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task ToggleRunningAsync()
    {
        if (_closing || _selecting || _transitioning)
        {
            return;
        }

        _transitioning = true;
        try
        {
            if (_running) await StopAsync();
            else await StartAsync();
        }
        finally
        {
            _transitioning = false;
        }
    }

    private Task StartAsync()
    {
        if (_running || _closing)
        {
            return Task.CompletedTask;
        }

        if (_settings.Area is not { } area)
        {
            MessageBox.Show(this, "Сначала выберите область. В игре нажмите Ctrl+Alt+F7, когда видно уведомление о сундуке.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        if (!SelectedScreen.Bounds.Contains(new Rectangle(area.X, area.Y, area.Width, area.Height)))
        {
            MessageBox.Show(this, "Выбранная область находится вне текущего монитора. Выберите её заново.", Text);
            return Task.CompletedTask;
        }

        _running = true;
        _gate.BreakObservationSequence();
        _startButton.Text = "Пауза";
        SetConfigurationEnabled(false);
        SetStatus("Загрузка OCR…", "Загрузка OCR");
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;
        long runId = ++_runId;
        var progress = new Progress<ScanReport>(report => HandleReport(report, runId));
        AppSettings configuration = _settings;
        _scanTask = Task.Run(() => ScanLoopAsync(configuration, progress, token), token);
        ApplyOverlay();
        return Task.CompletedTask;
    }

    private async Task StopAsync()
    {
        _running = false;
        ++_runId;
        _cancellation?.Cancel();
        if (_scanTask is { } task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
        }

        _scanTask = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _gate.BreakObservationSequence();
        _startButton.Text = "Начать";
        SetConfigurationEnabled(!_selecting && !_closing);
        SetStatus("Пауза", "Пауза");
    }

    private async Task ScanLoopAsync(AppSettings settings, IProgress<ScanReport> progress, CancellationToken token)
    {
        try
        {
            using var reader = new OcrReader(settings.Language, settings.BrightTextMask);
            CaptureArea area = settings.Area!;
            while (!token.IsCancellationRequested)
            {
                var duration = Stopwatch.StartNew();
                if (settings.OnlyWhenFortniteActive && !NativeMethods.IsFortniteActive())
                {
                    progress.Report(new ScanReport(null, null, false, _clock.Elapsed, 0, Waiting: true));
                    await Task.Delay(Math.Max(300, settings.ScanIntervalMs), token);
                    continue;
                }

                using Bitmap image = ScreenCapture.Capture(new Rectangle(area.X, area.Y, area.Width, area.Height));
                (IReadOnlyList<OcrLine> lines, ChestNotification? notification, bool visible, _) =
                    reader.ReadNotification(image, settings.RequireXp);
                token.ThrowIfCancellationRequested();
                progress.Report(new ScanReport(lines, notification, visible, _clock.Elapsed,
                    (int)duration.ElapsedMilliseconds));
                await Task.Delay(Math.Max(50, settings.ScanIntervalMs - (int)duration.ElapsedMilliseconds), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            progress.Report(new ScanReport(null, null, false, _clock.Elapsed, 0,
                Error: $"{ex.Message}\n\nПроверьте, что ZIP полностью распакован: рядом нужны папки tessdata и x64. " +
                    "Если не загружаются DLL OCR, установите Microsoft Visual C++ Redistributable x64 (2015–2022 или новее)."));
        }
    }

    private async void HandleReport(ScanReport report, long runId)
    {
        if (_closing || IsDisposed || !_running || runId != _runId)
        {
            return;
        }

        if (report.Error is { } error)
        {
            await StopAsync();
            MessageBox.Show(this, $"Распознавание остановлено.\n\n{error}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (report.Waiting)
        {
            _gate.BreakObservationSequence();
            SetStatus("Ожидание: перейдите в окно Fortnite", "Ожидание Fortnite");
            return;
        }

        _recognizedText.Text = string.Join(Environment.NewLine,
            report.Lines!.Select(line => $"{line.Confidence:P0}  {line.Text}"));
        if (_recognizedText.Text.Length == 0)
        {
            _recognizedText.Text = "Текст в выбранной области не найден.";
        }

        bool present = report.Notification is not null;
        SetStatus(present ? $"Уведомление о сундуке распознано · OCR {report.DurationMs} мс"
            : $"Распознавание работает · OCR {report.DurationMs} мс", "Работает");
        if (_gate.Observe(report.Visible, report.Notification?.Signature, report.Time))
        {
            await ChangeCountAsync(() =>
            {
                _ledger.Add(DateTimeOffset.Now, CountSource.Automatic);
                _lastCount.Text = $"Последний сундук: {DateTime.Now:HH:mm:ss} · автоматически";
            });
        }
    }

    private async Task ChangeCountAsync(Action action)
    {
        if (_closing || _selecting)
        {
            return;
        }

        try
        {
            action();
            UpdateCounts();
        }
        catch (Exception ex)
        {
            await StopAsync();
            MessageBox.Show(this, $"Не удалось сохранить статистику.\n\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Task UndoAsync() => ChangeCountAsync(() =>
    {
        if (_ledger.UndoLastInSession())
        {
            _gate.SuppressUntilClear();
            _lastCount.Text = "Последний сундук текущего матча отменён.";
        }
    });

    private Task NewMatchAsync() => ChangeCountAsync(() =>
    {
        _ledger.NewSession();
        _gate.SuppressUntilClear();
        _lastCount.Text = "Начат новый матч. Статистика за день и за всё время сохранена.";
    });

    private async Task SelectAreaAsync()
    {
        if (_selecting || _closing || _transitioning)
        {
            return;
        }

        _selecting = true;
        bool resume = _running;
        bool wasVisible = Visible;
        FormWindowState previousState = WindowState;
        IntPtr previousForeground = NativeMethods.GetForegroundWindow();
        bool returnToGame = NativeMethods.IsFortniteActive();
        try
        {
            await StopAsync();
            Hide();
            _overlay.Hide();
            await Task.Delay(180);
            Screen screen = SelectedScreen;
            using Bitmap screenshot = ScreenCapture.Capture(screen.Bounds);
            using var selector = new RegionSelectorForm(screen.Bounds, screenshot);
            if (selector.ShowDialog() == DialogResult.OK && selector.SelectedArea is { } area)
            {
                SaveSettings(_settings with { Area = area, MonitorDeviceName = screen.DeviceName });
                RefreshAreaLabel();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось выбрать область.\n\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (wasVisible && !_closing)
            {
                WindowState = previousState;
                Show();
            }

            _selecting = false;
            SetConfigurationEnabled(!_closing);
            ApplyOverlay();
            if (returnToGame && !_closing)
            {
                NativeMethods.SetForegroundWindow(previousForeground);
            }
        }

        if (resume && !_closing)
        {
            await StartAsync();
        }
    }

    private void SetConfigurationEnabled(bool enabled)
    {
        foreach (Control control in _configurationControls)
        {
            control.Enabled = enabled;
        }
    }

    private void SetStatus(string text, string overlayText)
    {
        _status.Text = text;
        _status.ForeColor = _running ? Theme.Accent : Theme.Muted;
        _overlayStatus = overlayText;
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        CounterSnapshot snapshot = _ledger.Snapshot(DateTimeOffset.Now);
        _session.Text = snapshot.Session.ToString();
        _today.Text = snapshot.Today.ToString();
        _total.Text = snapshot.Total.ToString();
        if (_statistics is { IsDisposed: false })
        {
            _statistics.RefreshCounts(snapshot.Total);
        }
        if (!_overlay.IsDisposed)
        {
            _overlay.UpdateCounts(snapshot, _overlayStatus);
        }
    }

    private void ShowStatistics()
    {
        if (_closing || _selecting) return;
        if (_statistics is null || _statistics.IsDisposed)
        {
            _statistics = new StatisticsForm(_ledger);
            _statistics.FormClosed += (_, _) => _statistics = null;
            _statistics.Show(this);
        }
        else
        {
            if (_statistics.WindowState == FormWindowState.Minimized)
            {
                _statistics.WindowState = FormWindowState.Normal;
            }

            _statistics.Activate();
        }
    }

    private void ToggleOverlay()
    {
        if (_closing || _selecting)
        {
            return;
        }

        SaveSettings(_settings with { ShowOverlay = !_settings.ShowOverlay });
        ApplyOverlay();
    }

    private void ApplyOverlay()
    {
        if (_closing || _selecting || _overlay.IsDisposed)
        {
            return;
        }

        _overlay.Place(SelectedScreen, _settings.OverlayCorner);
        _overlayButton.Text = _settings.ShowOverlay ? "Скрыть счётчик" : "Показать счётчик";
        if (_settings.ShowOverlay) _overlay.Show();
        else _overlay.Hide();
        UpdateCounts();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _registeredHotkeys.Clear();
        NativeMethods.ExcludeFromCapture(Handle);
        var unavailable = new List<string>();
        (int Id, string Label, uint Key)[] fixedHotkeys =
        [
            (7, "Ctrl+Alt+F7", (uint)Keys.F7),
            (8, "Ctrl+Alt+F8", (uint)Keys.F8),
            (10, "Ctrl+Alt+F10", (uint)Keys.F10),
            (11, "Ctrl+Alt+F11", (uint)Keys.F11),
            (12, "Ctrl+Alt+F12", (uint)Keys.F12)
        ];
        foreach (var hotkey in fixedHotkeys)
        {
            if (NativeMethods.RegisterHotKey(Handle, hotkey.Id, NativeMethods.HotkeyModifiers, hotkey.Key))
            {
                _registeredHotkeys.Add(hotkey.Id);
            }
            else
            {
                unavailable.Add(hotkey.Label);
            }
        }

        if (!RegisterAddChestHotkey(_settings.AddChestHotkey))
        {
            unavailable.Add(FormatHotkey(_settings.AddChestHotkey));
        }

        _hotkeyInfo.Text = unavailable.Count > 0
            ? "Заняты горячие клавиши: " + string.Join(", ", unavailable) + ". Используйте кнопки окна."
            : "Горячие клавиши активны, в том числе когда окно счётчика свёрнуто.";
        UpdateAddHotkeyButton();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        foreach (int id in _registeredHotkeys)
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }

        _registeredHotkeys.Clear();
        if (_addHotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, AddChestHotkeyId);
            _addHotkeyRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey && !_closing && !_selecting)
        {
            HandleHotkey(m.WParam.ToInt32());
            return;
        }

        base.WndProc(ref m);
    }

    private async void HandleHotkey(int id)
    {
        switch (id)
        {
            case 7: await SelectAreaAsync(); break;
            case 8: await ToggleRunningAsync(); break;
            case 9:
                await ChangeCountAsync(() =>
                {
                    _ledger.Add(DateTimeOffset.Now, CountSource.Manual);
                    _gate.SuppressUntilClear();
                    _lastCount.Text = $"Последний сундук: {DateTime.Now:HH:mm:ss} · добавлен вручную";
                });
                break;
            case 10: await UndoAsync(); break;
            case 11: await NewMatchAsync(); break;
            case 12: ToggleOverlay(); break;
        }
    }

    private bool RegisterAddChestHotkey(HotkeyBinding binding)
    {
        if (!IsHandleCreated)
        {
            _addHotkeyRegistered = false;
            return false;
        }

        if (_addHotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, AddChestHotkeyId);
            _addHotkeyRegistered = false;
        }

        _addHotkeyRegistered = NativeMethods.RegisterHotKey(Handle, AddChestHotkeyId,
            ToHotkeyModifiers(binding.Modifiers), (uint)binding.Key);
        return _addHotkeyRegistered;
    }

    private void ApplyAddChestHotkey(HotkeyBinding binding)
    {
        if (!binding.IsValid)
        {
            MessageBox.Show(this, "Эту клавишу нельзя назначить. Выберите обычную клавишу, например F8 или K.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateAddHotkeyButton();
            return;
        }

        if (IsReservedHotkey(binding))
        {
            MessageBox.Show(this, $"Сочетание {FormatHotkey(binding)} уже используется программой. Выберите другое.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateAddHotkeyButton();
            return;
        }

        if (binding.Modifiers == 0)
        {
            DialogResult answer = MessageBox.Show(this,
                $"Клавиша {FormatHotkey(binding)} без Ctrl, Alt или Shift будет перехватываться во всех программах, включая чат и меню. Продолжить?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                UpdateAddHotkeyButton();
                return;
            }
        }

        HotkeyBinding previous = _settings.AddChestHotkey;
        if (binding == previous)
        {
            UpdateAddHotkeyButton();
            return;
        }

        if (IsHandleCreated && !RegisterAddChestHotkey(binding))
        {
            RegisterAddChestHotkey(previous);
            MessageBox.Show(this, $"Не удалось назначить {FormatHotkey(binding)}: сочетание уже занято другой программой.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateAddHotkeyButton();
            return;
        }

        if (!SaveSettings(_settings with { AddChestHotkey = binding }))
        {
            RegisterAddChestHotkey(previous);
            UpdateAddHotkeyButton();
            return;
        }

        UpdateAddHotkeyButton();
        _hotkeyInfo.Text = $"Клавиша «+1»: {FormatHotkey(binding)}. Горячие клавиши работают и когда окно свёрнуто.";
    }

    private void UpdateAddHotkeyButton()
    {
        _addKeyButton.Text = $"{FormatHotkey(_settings.AddChestHotkey)} · изменить";
    }

    private static bool IsReservedHotkey(HotkeyBinding binding)
    {
        if (binding.Modifiers != (HotkeyBinding.Control | HotkeyBinding.Alt))
        {
            return false;
        }

        return (Keys)binding.Key is Keys.F7 or Keys.F8 or Keys.F10 or Keys.F11 or Keys.F12;
    }

    private static uint ToHotkeyModifiers(int modifiers)
    {
        uint result = 0x4000; // MOD_NOREPEAT
        if ((modifiers & HotkeyBinding.Alt) != 0) result |= 0x0001;
        if ((modifiers & HotkeyBinding.Control) != 0) result |= 0x0002;
        if ((modifiers & HotkeyBinding.Shift) != 0) result |= 0x0004;
        if ((modifiers & HotkeyBinding.Win) != 0) result |= 0x0008;
        return result;
    }

    private static string FormatHotkey(HotkeyBinding binding)
    {
        var parts = new List<string>();
        if ((binding.Modifiers & HotkeyBinding.Control) != 0) parts.Add("Ctrl");
        if ((binding.Modifiers & HotkeyBinding.Alt) != 0) parts.Add("Alt");
        if ((binding.Modifiers & HotkeyBinding.Shift) != 0) parts.Add("Shift");
        if ((binding.Modifiers & HotkeyBinding.Win) != 0) parts.Add("Win");
        parts.Add(FormatKey((Keys)binding.Key));
        return string.Join("+", parts);
    }

    private static string FormatKey(Keys key)
    {
        if (key is >= Keys.D0 and <= Keys.D9) return ((char)('0' + (key - Keys.D0))).ToString();
        if (key is >= Keys.A and <= Keys.Z) return key.ToString();
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9) return $"Num {key - Keys.NumPad0}";
        return key switch
        {
            Keys.Oemplus => "+",
            Keys.OemMinus => "-",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Oemtilde => "`",
            _ => key.ToString()
        };
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        _statsTimer.Stop();
        await StopAsync();
        _statsTimer.Dispose();
        _statistics?.Close();
        _overlay.Close();
        _overlay.Dispose();
        _allowClose = true;
        // StopAsync can finish synchronously when paused; close after this event has returned.
        BeginInvoke(new Action(Close));
    }
}