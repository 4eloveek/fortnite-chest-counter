using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FortniteChestCounter.Windows;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const uint HotkeyModifiers = 0x0001 | 0x0002 | 0x4000; // Alt + Ctrl + no-repeat

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, uint rasterOperation);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr window);

    public static bool IsFortniteActive()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == Environment.ProcessId)
        {
            return false;
        }

        var text = new StringBuilder(512);
        GetWindowText(window, text, text.Capacity);
        if (!text.ToString().Contains("Fortnite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return IsFortniteProcessName(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool IsFortniteProcessName(string name) =>
        name.StartsWith("FortniteClient-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Fortnite", StringComparison.OrdinalIgnoreCase);

    public static void ExcludeFromCapture(IntPtr window)
    {
        if (!SetWindowDisplayAffinity(window, 0x11))
        {
            SetWindowDisplayAffinity(window, 0x01);
        }
    }
}
