namespace FortniteChestCounter.Core;

public sealed record CaptureArea(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width >= 50 && Height >= 20 && Width <= 16384 && Height <= 16384;
}

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public sealed record AppSettings
{
    public string MonitorDeviceName { get; init; } = "";
    public CaptureArea? Area { get; init; }
    public string Language { get; init; } = "rus+eng";
    public int ScanIntervalMs { get; init; } = 200;
    public bool RequireXp { get; init; } = true;
    public bool BrightTextMask { get; init; } = true;
    public bool OnlyWhenFortniteActive { get; init; } = true;
    public bool ShowOverlay { get; init; } = true;
    public OverlayCorner OverlayCorner { get; init; } = OverlayCorner.TopLeft;
    public HotkeyBinding AddChestHotkey { get; init; } = HotkeyBinding.DefaultAddChest;

    public void Validate()
    {
        if (MonitorDeviceName is null || Language is not ("rus" or "eng" or "rus+eng") ||
            ScanIntervalMs is < 100 or > 2000 || (Area is not null && !Area.IsValid) ||
            !Enum.IsDefined(OverlayCorner) || AddChestHotkey is null)
        {
            throw new InvalidDataException("Некорректные настройки. Проверьте settings.json.");
        }

        AddChestHotkey.Validate();
    }
}