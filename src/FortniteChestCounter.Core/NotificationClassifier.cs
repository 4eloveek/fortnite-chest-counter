using System.Text;
using System.Text.RegularExpressions;

namespace FortniteChestCounter.Core;

public sealed record OcrLine(string Text, float Confidence, int X, int Y, int Width, int Height)
{
    public double CenterX => X + Width / 2.0;
    public double CenterY => Y + Height / 2.0;
}

public sealed record ChestNotification(string Text, float Confidence, ChestSignature Signature);

/// <summary>
/// Identifies one showing of a chest label: its canonical word and its centre on
/// screen. OCR boxes jitter by a few pixels from scan to scan, so two readings of
/// the same showing match within <see cref="Tolerance"/>; a HUD row shift or a new
/// showing moves the label by a full row and does not match.
/// </summary>
public sealed record ChestSignature(string Label, double X, double Y)
{
    public const double Tolerance = 12;

    public bool Matches(ChestSignature other) =>
        Label == other.Label && Math.Abs(X - other.X) <= Tolerance && Math.Abs(Y - other.Y) <= Tolerance;
}

public static partial class NotificationClassifier
{
    // The screen keeps showing a chest label while its XP line may be misread.
    // MainForm uses this signal to distinguish a real disappearance from an OCR miss.
    public static bool ContainsChestLabel(IReadOnlyList<OcrLine> lines)
    {
        foreach (OcrLine line in lines)
        {
            if (line.Confidence < 0.20f)
            {
                continue;
            }

            string text = Normalize(line.Text);
            if (ChestWord().IsMatch(text) && !QuestWords().IsMatch(text))
            {
                return true;
            }
        }

        return false;
    }

    public static ChestNotification? Find(IReadOnlyList<OcrLine> lines, bool requireXp)
    {
        foreach (OcrLine chest in lines)
        {
            string text = Normalize(chest.Text);
            if (chest.Confidence < 0.40f || !ChestWord().IsMatch(text) || QuestWords().IsMatch(text))
            {
                continue;
            }

            if (!requireXp)
            {
                return new ChestNotification(chest.Text, chest.Confidence, BuildSignature(chest));
            }

            // The award must be next to the chest label, not elsewhere on the screen.
            string[] nearbyTexts = lines
                .Where(line => line.Confidence >= 0.20f &&
                    Math.Abs(line.CenterX - chest.CenterX) <= Math.Max(140, chest.Width * 1.8) &&
                    Math.Abs(line.CenterY - chest.CenterY) <= Math.Max(64, chest.Height * 4))
                .OrderBy(line => line.Y)
                .Select(line => Normalize(line.Text))
                .ToArray();
            string nearbyText = string.Join(" ", nearbyTexts);

            bool standardAward = XpWord().IsMatch(nearbyText) &&
                (PositiveGain().IsMatch(nearbyText) || NumberWithXp().IsMatch(nearbyText));
            // Fortnite's italic ОП is sometimes read as 0П or 0n. Do not treat
            // these as generic XP words: require +amount on the same nearby line
            // and an actual Russian СУНДУК label with the usual confidence checks.
            bool ambiguousRussianAward = RussianChestWord().IsMatch(text) &&
                nearbyTexts.Any(line => AmbiguousRussianAward().IsMatch(line));

            if (standardAward || ambiguousRussianAward)
            {
                return new ChestNotification(nearbyText, chest.Confidence, BuildSignature(chest));
            }
        }

        return null;
    }

    private static ChestSignature BuildSignature(OcrLine chest)
    {
        string text = Normalize(chest.Text);
        Match label = ChestWord().Match(text);
        return new ChestSignature(label.Success ? label.Value : text, chest.CenterX, chest.CenterY);
    }

    private static string Normalize(string text) => text.Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:CHEST|СУНДУК)(?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex ChestWord();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])СУНДУК(?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex RussianChestWord();

    [GeneratedRegex(@"ОБЫЩИТЕ|НАЙДИТЕ|ЗАДАНИ|ИСПЫТАНИ|QUEST|SEARCH\s+(?:A\s+|THE\s+)?CHEST", RegexOptions.CultureInvariant)]
    private static partial Regex QuestWords();

    [GeneratedRegex(@"(?<!\p{L})(?:XP|ХР|ХP|XР|ОП(?:ЫТ(?:А)?)?)(?!\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex XpWord();

    [GeneratedRegex(@"\+\s*[0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex PositiveGain();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[0-9][0-9\s.,]*\s*(?:XP|ХР|ХP|XР|ОП(?:ЫТ(?:А)?)?)(?!\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex NumberWithXp();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\+\s*[0-9][0-9\s.,]*\s*[ОO0][ПN](?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex AmbiguousRussianAward();
}