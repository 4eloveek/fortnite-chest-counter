using System.Collections;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

// Test-only startup hook, never shipped inside the release EXE.
// The actual self-contained EXE loads this before Main. It tests the extracted
// payload, then exits before the program's normal mutex and real ledger loading.
internal static class StartupHook
{
    private static readonly List<object> Checks = [];
    private static string Output => Environment.GetEnvironmentVariable("FCC_VERIFY_OUTPUT")!;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    public static void Initialize()
    {
        int exitCode = 1;
        var thread = new Thread(() =>
        {
            Directory.CreateDirectory(Output);
            try
            {
                Verify();
                Save(true, null);
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Save(false, ex.ToString());
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Environment.Exit(exitCode);
    }

    private static void Verify()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string appRoot = AppContext.BaseDirectory;
        string executableDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
        string extractRoot = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")!;
        Require(Under(appRoot, extractRoot) && !Under(appRoot, executableDirectory), "AppContext points to the extracted bundle, not the EXE directory");
        Require(Directory.GetFiles(executableDirectory, "*", SearchOption.AllDirectories).Length == 1, "EXE folder contains exactly one file");
        foreach (string path in new[]
        {
            "FortniteChestCounter.dll", "FortniteChestCounter.Core.dll", "Tesseract.dll",
            "System.Private.CoreLib.dll", "System.Windows.Forms.dll", "FortniteChestCounter.runtimeconfig.json",
            "x64/tesseract50.dll", "x64/leptonica-1.82.0.dll", "tessdata/eng.traineddata", "tessdata/rus.traineddata",
            "licenses/Tesseract-Apache-2.0.txt", "licenses/Leptonica-BSD.txt", "licenses/dotnet-MIT.txt",
            "licenses/FortniteChestCounter-MIT.txt", "THIRD-PARTY-NOTICES.md", "README.md"
        })
        {
            Require(File.Exists(Path.Combine(appRoot, path)), "Bundled file present: " + path);
        }

        Require(Hash(Path.Combine(appRoot, "tessdata/eng.traineddata")) == "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2", "English model SHA256 matches original");
        Require(Hash(Path.Combine(appRoot, "tessdata/rus.traineddata")) == "e16e5e036cce1d9ec2b00063cf8b54472625b9e14d893a169e2b0dedeb4df225", "Russian model SHA256 matches original");
        Require(Under(typeof(object).Assembly.Location, appRoot), "CoreLib loaded from the bundle, not installed .NET");
        // The SDK's singlefilehost links native CoreCLR into the EXE; no loose coreclr.dll is required.
        Require(RuntimeInformation.FrameworkDescription == ".NET 10.0.15", "Self-contained runtime is .NET 10.0.15");
        using (JsonDocument config = JsonDocument.Parse(File.ReadAllText(Path.Combine(appRoot, "FortniteChestCounter.runtimeconfig.json"))))
        {
            Require(config.RootElement.GetProperty("runtimeOptions").GetProperty("includedFrameworks").EnumerateArray()
                .All(framework => framework.GetProperty("version").GetString() == "10.0.15"), "Bundled .NET runtime 10.0.15");
        }

        Assembly app = Assembly.LoadFrom(Path.Combine(appRoot, "FortniteChestCounter.dll"));
        Assembly core = Assembly.Load("FortniteChestCounter.Core");
        Require(app.GetName().Version!.ToString(3) == "0.2.0", "Release program version 0.2.0");
        Require(Under(app.Location, appRoot) && Under(core.Location, appRoot), "Application and Core loaded from extracted payload");
        Type captureType = app.GetType("FortniteChestCounter.Windows.ScreenCapture", true)!;
        MethodInfo capture = captureType.GetMethod("Capture")!;
        Rectangle screen = Screen.PrimaryScreen!.Bounds;
        Rectangle region = new(screen.Left + 400, screen.Top + 400, 240, 80);
        using (Bitmap bitmap = (Bitmap)capture.Invoke(null, [region])!)
            Require(bitmap.Size == region.Size, "Real GDI capture through bundled DLL");
        using Process process = Process.GetCurrentProcess();
        uint before = GetGuiResources(process.Handle, 0);
        for (int i = 0; i < 12; i++) using (Bitmap bitmap = (Bitmap)capture.Invoke(null, [region])!) { }
        uint after = GetGuiResources(process.Handle, 0);
        Require(before == after, "Twelve captures do not grow GDI resources");
        Checks.Add(new { Test = "GDI resources", Before = before, After = after });
        bool rejected = false;
        try { using Bitmap bitmap = (Bitmap)capture.Invoke(null, [new Rectangle(0, 0, 0, 20)])!; }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { rejected = true; }
        Require(rejected, "Invalid capture area rejected");

        Type readerType = app.GetType("FortniteChestCounter.Windows.OcrReader", true)!;
        MethodInfo read = readerType.GetMethod("ReadNotification")!;
        foreach (string language in new[] { "eng", "rus", "rus+eng" })
        {
            foreach (bool mask in new[] { true, false })
            {
                using IDisposable reader = (IDisposable)Activator.CreateInstance(readerType, language, mask)!;
                string title = language == "eng" ? "CHEST" : "СУНДУК";
                string xp = language == "rus" ? "+100 ОПЫТА" : "+100 XP";
                foreach (Color color in new[] { Color.White, Color.Gold, Color.FromArgb(200, 150, 255) })
                {
                    using Bitmap image = Synthetic(title, xp, color);
                    ITuple result = (ITuple)read.Invoke(reader, [image, true])!;
                    Require(result[1] is not null, $"Bundled OCR: {language}, mask={mask}, color={color.ToArgb()}");
                }
                if (language == "eng" && mask)
                {
                    using Bitmap quest = Synthetic("SEARCH A CHEST", "+100 XP", Color.White);
                    Require(((ITuple)read.Invoke(reader, [quest, true])!)[1] is null, "Quest excluded by bundled OCR/classifier");
                    using Bitmap blank = new(560, 160);
                    using (Graphics graphics = Graphics.FromImage(blank)) graphics.Clear(Color.FromArgb(24, 32, 48));
                    Require(((ITuple)read.Invoke(reader, [blank, true])!)[1] is null, "Blank image excluded by bundled OCR/classifier");
                }
            }
        }

        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName.Equals("tesseract50.dll", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Equals("leptonica-1.82.0.dll", StringComparison.OrdinalIgnoreCase))
            {
                Require(Under(module.FileName, Path.Combine(appRoot, "x64")), "Native OCR library loaded from bundle: " + module.ModuleName);
            }
        }

        string? sample = Environment.GetEnvironmentVariable("FCC_VERIFY_SAMPLE");
        if (!string.IsNullOrEmpty(sample))
        {
            using Bitmap frame = new(sample);
            using Bitmap crop = frame.Clone(new Rectangle(1217, 282, 220, 125), PixelFormat.Format24bppRgb);
            using IDisposable reader = (IDisposable)Activator.CreateInstance(readerType, "rus+eng", true)!;
            Require(((ITuple)read.Invoke(reader, [crop, true])!)[1] is not null,
                "Saved real chest PNG accepted by the single-file OCR path");
        }

        VerifyLedgerAndWindows(app, core);
    }

    private static void VerifyLedgerAndWindows(Assembly app, Assembly core)
    {
        IntPtr foreground = GetForegroundWindow();
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        string path = Path.Combine(Output, "fixture-" + Guid.NewGuid().ToString("N") + ".json");
        Type ledgerType = core.GetType("FortniteChestCounter.Core.CounterLedger", true)!;
        Type sourceType = core.GetType("FortniteChestCounter.Core.CountSource", true)!;
        object ledger = Activator.CreateInstance(ledgerType, path)!;
        MethodInfo add = ledgerType.GetMethod("Add")!;
        add.Invoke(ledger, [DateTimeOffset.Now.AddDays(-1), Enum.Parse(sourceType, "Automatic")]);
        add.Invoke(ledger, [DateTimeOffset.Now, Enum.Parse(sourceType, "Manual")]);
        Require(Value<int>(ledgerType.GetMethod("Snapshot")!.Invoke(ledger, [DateTimeOffset.Now])!, "Total") == 2, "Fixture ledger counts both sources");
        object restored = Activator.CreateInstance(ledgerType, path)!;
        Require(Value<int>(ledgerType.GetMethod("Snapshot")!.Invoke(restored, [DateTimeOffset.Now])!, "Total") == 2, "Fixture statistics restored from JSON");
        object[] days = ((IEnumerable)ledgerType.GetMethod("DailyCounts")!.Invoke(ledger, [today.AddDays(-6), today])!).Cast<object>().ToArray();
        Require(days.Length == 7 && days.Sum(day => Value<int>(day, "Count")) == 2, "Bundled daily history includes gaps and the full period");

        Type statsType = app.GetType("FortniteChestCounter.Windows.StatisticsForm", true)!;
        using (Form statistics = (Form)Activator.CreateInstance(statsType, ledger)!)
        {
            ShowOffscreenWithoutActivation(statistics);
            ComboBox period = Field<ComboBox>(statistics, "_period");
            Control chart = Field<Control>(statistics, "_chart");
            Require(ChartDays(chart).Length == 7, "Bundled graph default seven-day period");
            period.SelectedIndex = 1;
            Require(ChartDays(chart).Length == 30, "Bundled graph thirty-day period");
            period.SelectedIndex = 3;
            Require(ChartDays(chart).Sum(day => Value<int>(day, "Count")) == 2, "Bundled graph all-time total");
            period.SelectedIndex = 4;
            Field<DateTimePicker>(statistics, "_from").Value = DateTime.Today.AddDays(-1);
            Field<DateTimePicker>(statistics, "_through").Value = DateTime.Today;
            Require(ChartDays(chart).Length == 2, "Bundled graph custom inclusive dates");
            period.SelectedIndex = 0;
            statistics.PerformLayout();
            foreach (Control control in statistics.Controls) control.PerformLayout();
            using Bitmap image = new(statistics.Width, statistics.Height);
            statistics.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(Output, "fixture-statistics.png"), ImageFormat.Png);
            Require(AccentPixels(image) > 100, "Daily columns are actually rendered by bundled WinForms");
            Require(GetWindowDisplayAffinity(statistics.Handle, out uint affinity) && affinity == 0x11, "Bundled graph excluded from screen capture");
            Require(statistics.Left < -10000, "Graph rendered offscreen, outside the user's desktop");
            ledgerType.GetMethod("UndoLastInSession")!.Invoke(ledger, null);
            statsType.GetMethod("RefreshCounts")!.Invoke(statistics, [1]);
            Require(ChartDays(chart).Sum(day => Value<int>(day, "Count")) == 1, "Bundled graph refreshes after fixture undo");
            statistics.Close();
            Application.DoEvents();
        }

        Type settingsType = core.GetType("FortniteChestCounter.Core.AppSettings", true)!;
        object settings = Activator.CreateInstance(settingsType)!;
        settingsType.GetProperty("ShowOverlay")!.SetValue(settings, false);
        Type mainType = app.GetType("FortniteChestCounter.Windows.MainForm", true)!;
        using (Form main = (Form)Activator.CreateInstance(mainType, settings, Path.Combine(Output, "unused-settings.json"), ledger)!)
        {
            ShowOffscreenWithoutActivation(main);
            main.PerformLayout();
            foreach (Control control in main.Controls) control.PerformLayout();
            using Bitmap image = new(main.Width, main.Height);
            main.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(Output, "fixture-main.png"), ImageFormat.Png);
            Require(AccentPixels(image) > 100, "Main counters and controls are actually rendered");
            Require(Descendants(main).OfType<Button>().Any(button => button.Text == "Статистика по дням"), "Main window contains the daily statistics button");
            Require(GetWindowDisplayAffinity(main.Handle, out uint affinity) && affinity == 0x11, "Bundled main window excluded from capture");
            Require(main.Left < -10000, "Main window rendered offscreen, outside the user's desktop");
            main.Close();
            Application.DoEvents();
        }
        Require(GetForegroundWindow() == foreground, "GUI verification did not change the foreground window");

        using (Form overlay = (Form)Activator.CreateInstance(app.GetType("FortniteChestCounter.Windows.OverlayForm", true)!)!)
        {
            ShowOffscreenWithoutActivation(overlay);
            using Bitmap image = new(overlay.Width, overlay.Height);
            overlay.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(Output, "fixture-overlay.png"), ImageFormat.Png);
            Require(GetWindowDisplayAffinity(overlay.Handle, out uint affinity) && affinity == 0x11, "Bundled overlay created and excluded from capture");
            overlay.Close();
            Application.DoEvents();
        }
    }

    private static void ShowOffscreenWithoutActivation(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.ShowInTaskbar = false;
        form.Enabled = false;
        IntPtr handle = form.Handle;
        SetWindowLongPtr(handle, -20, new IntPtr(GetWindowLongPtr(handle, -20).ToInt64() | 0x08000000L));
        form.Show();
        form.Enabled = true;
        Application.DoEvents();
    }

    private static int AccentPixels(Bitmap image)
    {
        int count = 0;
        int accent = Color.FromArgb(100, 225, 188).ToArgb();
        for (int y = 0; y < image.Height; y += 2)
            for (int x = 0; x < image.Width; x += 2)
                if (image.GetPixel(x, y).ToArgb() == accent) count++;
        return count;
    }

    private static Bitmap Synthetic(string title, string xp, Color color)
    {
        var image = new Bitmap(560, 160);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(24, 32, 48));
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var xpFont = new Font("Arial", 28, FontStyle.Bold, GraphicsUnit.Pixel);
        using var titleFont = new Font("Arial", 26, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        graphics.DrawString(xp, xpFont, brush, 30, 24);
        graphics.DrawString(title, titleFont, brush, 30, 74);
        return image;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static bool Under(string path, string directory) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static T Value<T>(object target, string property) => (T)target.GetType().GetProperty(property)!.GetValue(target)!;
    private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object[] ChartDays(Control chart) => ((IEnumerable)Field<object>(chart, "_days")).Cast<object>().ToArray();
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
    private static void Require(bool passed, string test)
    {
        if (!passed) throw new InvalidOperationException(test);
        Checks.Add(new { Test = test, Passed = true });
    }
    private static void Save(bool passed, string? error) => File.WriteAllText(Path.Combine(Output, "verification.json"), JsonSerializer.Serialize(new
    {
        Time = DateTimeOffset.Now,
        Passed = passed,
        Runtime = RuntimeInformation.FrameworkDescription,
        Process = Environment.ProcessPath,
        BaseDirectory = AppContext.BaseDirectory,
        HookLocation = typeof(StartupHook).Assembly.Location,
        Checks,
        Note = "Actual release EXE with a test-only external startup hook. User Main and user ledger loading never execute; only private fixture state is modified. Windows desktop capture is not saved. Not a live Fortnite test.",
        Error = error
    }, new JsonSerializerOptions { WriteIndented = true }));
}
