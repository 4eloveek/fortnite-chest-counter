using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FortniteChestCounter.Core;
using Tesseract;

namespace FortniteChestCounter.Windows;

internal sealed class OcrReader : IDisposable
{
    private const int Scale = 2;
    private const int Padding = 16;
    private readonly TesseractEngine _engine;
    private readonly bool _brightTextMask;

    public OcrReader(string language, bool brightTextMask)
    {
        _brightTextMask = brightTextMask;
        string dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _engine = new TesseractEngine(dataPath, language, EngineMode.LstmOnly);
        _engine.SetVariable("user_defined_dpi", 200);
        _engine.SetVariable("tessedit_do_invert", !brightTextMask);
    }

    public IReadOnlyList<OcrLine> Read(Bitmap source)
    {
        using Bitmap prepared = Prepare(source, _brightTextMask);
        return ReadPrepared(prepared, PageSegMode.SparseText);
    }

    public (IReadOnlyList<OcrLine> Lines, ChestNotification? Notification, string Mode) ReadNotification(
        Bitmap source, bool requireXp)
    {
        using Bitmap prepared = Prepare(source, _brightTextMask);
        IReadOnlyList<OcrLine> sparseLines = ReadPrepared(prepared, PageSegMode.SparseText);
        ChestNotification? sparseNotification = NotificationClassifier.Find(sparseLines, requireXp);
        if (sparseNotification is not null)
        {
            return (sparseLines, sparseNotification, "SparseText");
        }

        // Live Fortnite frames can lose the smaller bold caption in SparseText.
        // Evaluate a second complete reading of the same frame, never stitching
        // XP from one pass to a chest label from another pass.
        IReadOnlyList<OcrLine> blockLines = ReadPrepared(prepared, PageSegMode.SingleBlock);
        ChestNotification? blockNotification = NotificationClassifier.Find(blockLines, requireXp);
        return blockNotification is not null
            ? (blockLines, blockNotification, "SingleBlock")
            : (sparseLines, null, "SparseText + SingleBlock (no match)");
    }

    private IReadOnlyList<OcrLine> ReadPrepared(Bitmap prepared, PageSegMode mode)
    {
        using var buffer = new MemoryStream();
        prepared.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
        using Pix pix = Pix.LoadFromMemory(buffer.ToArray());
        using Page page = _engine.Process(pix, mode);
        using ResultIterator iterator = page.GetIterator();
        var lines = new List<OcrLine>();
        iterator.Begin();
        do
        {
            string? text = iterator.GetText(PageIteratorLevel.TextLine);
            if (!string.IsNullOrWhiteSpace(text) &&
                iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out Tesseract.Rect bounds))
            {
                lines.Add(new OcrLine(text.Trim(), iterator.GetConfidence(PageIteratorLevel.TextLine) / 100f,
                    (bounds.X1 - Padding) / Scale, (bounds.Y1 - Padding) / Scale,
                    bounds.Width / Scale, bounds.Height / Scale));
            }
        } while (iterator.Next(PageIteratorLevel.TextLine));

        return lines;
    }

    private static Bitmap Prepare(Bitmap source, bool brightTextMask)
    {
        var result = new Bitmap(source.Width * Scale + Padding * 2,
            source.Height * Scale + Padding * 2, PixelFormat.Format24bppRgb);
        try
        {
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(brightTextMask ? Color.Black : Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, new Rectangle(Padding, Padding,
                    source.Width * Scale, source.Height * Scale));
            }

            if (brightTextMask)
            {
                ApplyBrightTextMask(result);
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static unsafe void ApplyBrightTextMask(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        try
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    byte* pixel = row + x * 3;
                    int b = pixel[0], g = pixel[1], r = pixel[2];
                    int maximum = Math.Max(r, Math.Max(g, b));
                    int minimum = Math.Min(r, Math.Min(g, b));
                    bool white = maximum >= 170 && minimum >= 120 && maximum - minimum <= 80;
                    bool gold = r >= 175 && g >= 145 && b <= 160;
                    bool purple = b >= 185 && r >= 135 && g >= 90;
                    byte value = (byte)(white || gold || purple ? 0 : 255);
                    pixel[0] = pixel[1] = pixel[2] = value;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public void Dispose() => _engine.Dispose();
}
