using System.Text;
using System.Text.RegularExpressions;

namespace FortniteChestCounter.Core;

public sealed record OcrLine(string Text, float Confidence, int X, int Y, int Width, int Height)
{
    public double CenterX => X + Width / 2.0;
    public double CenterY => Y + Height / 2.0;
}

public sealed record ChestNotification(string Text, float Confidence);

public static partial class NotificationClassifier
{
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
                return new ChestNotification(chest.Text, chest.Confidence);
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
                return new ChestNotification(nearbyText, chest.Confidence);
            }
        }

        return null;
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
