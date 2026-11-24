using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FortniteChestCounter.Windows;

internal static class ScreenCapture
{
    // Graphics.CopyFromScreen rejects combined CopyPixelOperation values.
    // BitBlt accepts SRCCOPY | CAPTUREBLT and includes layered windows.
    private const uint CaptureRasterOperation = 0x00CC0020 | 0x40000000;

    public static Bitmap Capture(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            throw new ArgumentException("Область захвата не выбрана.", nameof(area));
        }

        var image = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(image);
            IntPtr sourceDc = NativeMethods.GetDC(IntPtr.Zero);
            if (sourceDc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Не удалось получить контекст рабочего стола для захвата экрана.");
            }

            try
            {
                IntPtr destinationDc = graphics.GetHdc();
                try
                {
                    if (!NativeMethods.BitBlt(destinationDc, 0, 0, area.Width, area.Height,
                        sourceDc, area.X, area.Y, CaptureRasterOperation))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось захватить экран через GDI.");
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(destinationDc);
                }
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, sourceDc);
            }

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }
}
