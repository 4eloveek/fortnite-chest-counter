using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(true, @"Local\FortniteChestCounter", out bool created);
        if (!created)
        {
            MessageBox.Show("Счётчик уже запущен. Найдите его окно на панели задач.", "Счётчик сундуков");
            return;
        }

        try
        {
            string dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FortniteChestCounter");
            string settingsPath = Path.Combine(dataDirectory, "settings.json");
            AppSettings settings = JsonFile.Read<AppSettings>(settingsPath) ?? new AppSettings();
            settings.Validate();
            var ledger = new CounterLedger(Path.Combine(dataDirectory, "state.json"));
            Application.Run(new MainForm(settings, settingsPath, ledger));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить счётчик.\n\n{ex.Message}", "Счётчик сундуков",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
