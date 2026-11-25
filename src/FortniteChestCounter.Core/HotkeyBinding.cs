namespace FortniteChestCounter.Core;

/// <summary>
/// A global hotkey: Win32 modifier flags plus a virtual-key code.
/// Core keeps no dependency on Windows Forms; the Windows project converts
/// <see cref="System.Windows.Forms.Keys"/> to and from this value.
/// </summary>
public sealed record HotkeyBinding(int Modifiers, int Key)
{
    public const int Alt = 0x0001;
    public const int Control = 0x0002;
    public const int Shift = 0x0004;
    public const int Win = 0x0008;

    private const int SupportedModifiers = Alt | Control | Shift | Win;

    /// <summary>Ctrl+Alt+F9 keeps the 0.2.0 manual +1 shortcut.</summary>
    public static HotkeyBinding DefaultAddChest { get; } = new(Control | Alt, 0x78);

    public bool IsValid =>
        Modifiers >= 0 &&
        (Modifiers & ~SupportedModifiers) == 0 &&
        Key is > 0 and <= 0xFE &&
        !IsModifierKey(Key);

    public void Validate()
    {
        if (!IsValid)
        {
            throw new InvalidDataException(
                "Некорректная горячая клавиша в settings.json. Укажите сочетание с обычной клавишей.");
        }
    }

    private static bool IsModifierKey(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;
}
