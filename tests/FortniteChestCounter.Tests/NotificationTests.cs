using FortniteChestCounter.Core;
using Xunit;

namespace FortniteChestCounter.Tests;

public sealed class NotificationTests
{
    private static OcrLine Line(string text, int x = 100, int y = 100, float confidence = 0.95f) =>
        new(text, confidence, x, y, 150, 25);

    [Theory]
    [InlineData("+100 XP", "CHEST")]
    [InlineData("+ 200 ХР", "RARE CHEST")]
    [InlineData("100 XP", "CHEST SEARCHED")]
    [InlineData("+100 ОП", "СУНДУК")]
    [InlineData("+250 ОПЫТА", "РЕДКИЙ СУНДУК")]
    [InlineData("+1 000 ОПЫТ", "СУНДУК ОБЫСКАН")]
    public void RecognizesLocalizedAwards(string award, string chest)
    {
        Assert.NotNull(NotificationClassifier.Find([Line(award, y: 70), Line(chest)], requireXp: true));
    }

    [Theory]
    [InlineData("+1 2000п")]
    [InlineData("+1 2000n")]
    [InlineData("+1 200 0П")]
    [InlineData("+1 200 0n")]
    [InlineData("+1 200 Оn")]
    [InlineData("+1 200 Oп")]
    [InlineData("+1 200 ON")]
    public void RecognizesAmbiguousOpOnlyWithRussianChestAndInlineGain(string award)
    {
        Assert.NotNull(NotificationClassifier.Find([Line(award, y: 70), Line("СУНДУК")], requireXp: true));
    }

    [Fact]
    public void RecognizesOcrLinesRecordedFromTheFortniteScreenshot()
    {
        OcrLine[] lines =
        [
            new("+1 2000n", 0.89f, 14, 0, 148, 55),
            new("СУНДУК", 0.96f, 129, 63, 71, 20)
        ];
        Assert.NotNull(NotificationClassifier.Find(lines, requireXp: true));
    }

    [Theory]
    [InlineData("0n")]
    [InlineData("1 2000n")]
    [InlineData("-1 2000n")]
    [InlineData("+1 200 N")]
    [InlineData("+1 2000n1")]
    [InlineData("+1 2000not")]
    [InlineData("+1 2000н")]
    [InlineData("+1 20доп")]
    [InlineData("+1 200 ДОП")]
    public void RejectsUnconfirmedOrUnboundedAmbiguousUnits(string award)
    {
        Assert.Null(NotificationClassifier.Find([Line(award, y: 70), Line("СУНДУК")], requireXp: true));
    }

    [Theory]
    [InlineData("CHEST")]
    [InlineData("CHESTS")]
    [InlineData("СУНДУКИ")]
    [InlineData("CHEST СУНДУКИ")]
    [InlineData("ОБЫЩИТЕ СУНДУК")]
    [InlineData("НАЙДИТЕ СУНДУК")]
    public void DoesNotExtendAmbiguousOpToEnglishLabelsOrQuests(string chest)
    {
        Assert.Null(NotificationClassifier.Find([Line("+1 2000n", y: 70), Line(chest)], requireXp: true));
    }

    [Fact]
    public void AmbiguousOpRequiresAmountOnTheSameLine()
    {
        Assert.Null(NotificationClassifier.Find(
            [Line("+1200", y: 50), Line("0n", y: 75), Line("СУНДУК")], requireXp: true));
    }

    [Fact]
    public void AmbiguousOpPreservesDistanceAndConfidenceChecks()
    {
        Assert.Null(NotificationClassifier.Find(
            [Line("+1 2000n", x: 900, y: 500), Line("СУНДУК")], requireXp: true));
        Assert.Null(NotificationClassifier.Find(
            [Line("+1 2000n", y: 70, confidence: 0.19f), Line("СУНДУК")], requireXp: true));
        Assert.Null(NotificationClassifier.Find(
            [Line("+1 2000n", y: 70), Line("СУНДУК", confidence: 0.39f)], requireXp: true));
        Assert.NotNull(NotificationClassifier.Find(
            [Line("+1 2000n", y: 70, confidence: 0.20f), Line("СУНДУК", confidence: 0.40f)], requireXp: true));
    }

    [Fact]
    public void ChangingAmbiguousOpSpellingDoesNotCountOneNotificationTwice()
    {
        var gate = new NotificationGate();
        string[] awards = ["+1 2000n", "+1 2000п", "+1 20доп", "+1 2000n", "+1 200 0П"];
        int count = 0;
        for (int frame = 0; frame < awards.Length; frame++)
        {
            ChestNotification? notification = NotificationClassifier.Find(
                [Line(awards[frame], y: 70), Line("СУНДУК")], requireXp: true);
            if (gate.Observe(notification is not null, notification?.Signature, At(frame * 200))) count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void RecognizesXpAndAmountOnSeparateLines()
    {
        Assert.NotNull(NotificationClassifier.Find(
            [Line("+100", y: 50), Line("XP", y: 75), Line("CHEST", y: 100)], requireXp: true));
    }

    [Theory]
    [InlineData("CHESTS")]
    [InlineData("CHESTPLATE")]
    [InlineData("SEARCH CHEST")]
    [InlineData("Search a chest")]
    [InlineData("СУНДУКИ")]
    [InlineData("ОБЫЩИТЕ СУНДУК")]
    [InlineData("НАЙДИТЕ СУНДУК")]
    public void RejectsQuestAndUnrelatedText(string text)
    {
        Assert.Null(NotificationClassifier.Find([Line("+100 XP", y: 70), Line(text)], requireXp: true));
    }

    [Fact]
    public void DoesNotTreatChestNameAloneAsAnOpening()
    {
        Assert.Null(NotificationClassifier.Find([Line("СУНДУК")], requireXp: true));
        Assert.NotNull(NotificationClassifier.Find([Line("СУНДУК")], requireXp: false));
    }

    [Fact]
    public void DoesNotUseAnAwardFromAnotherPartOfTheScreen()
    {
        Assert.Null(NotificationClassifier.Find(
            [Line("+100 XP", x: 900, y: 500), Line("CHEST")], requireXp: true));
    }

    [Fact]
    public void RejectsLowConfidenceChestRecognition()
    {
        Assert.Null(NotificationClassifier.Find(
            [Line("+100 XP", y: 70), Line("CHEST", confidence: 0.25f)], requireXp: true));
    }

    [Fact]
    public void RejectsXpWithoutAnAwardAmount()
    {
        Assert.Null(NotificationClassifier.Find([Line("XP", y: 70), Line("CHEST")], requireXp: true));
    }

    [Fact]
    public void CountsOneLongNotificationOnlyOnce()
    {
        var gate = new NotificationGate();
        int count = Enumerable.Range(0, 100).Count(frame => gate.Observe(true, First, At(frame * 200)));
        Assert.Equal(1, count);
    }

    [Fact]
    public void RejectsASingleSpuriousFrame()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.False(gate.Observe(false, null, At(200)));
        Assert.False(gate.Observe(true, First, At(400)));
        Assert.False(gate.Observe(false, null, At(600)));
    }

    [Fact]
    public void OneMissedFrameDoesNotDuplicateAnOpening()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(false, null, At(400)));
        Assert.False(gate.Observe(true, First, At(600)));
        Assert.False(gate.Observe(true, First, At(800)));
        Assert.False(gate.Observe(true, First, At(2000)));
    }

    [Fact]
    public void CountsASecondNotificationAfterAShortGap()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(false, null, At(400)));
        Assert.False(gate.Observe(false, null, At(600)));
        Assert.False(gate.Observe(true, First, At(800)));
        Assert.True(gate.Observe(true, First, At(1000)));
    }

    [Fact]
    public void RearmsAfterTheNotificationActuallyDisappears()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(false, null, At(400)));
        Assert.False(gate.Observe(false, null, At(1400)));
        Assert.False(gate.Observe(true, First, At(1600)));
        Assert.True(gate.Observe(true, First, At(1800)));
    }

    [Fact]
    public void EnforcesMinimumIntervalEvenWithFastRearming()
    {
        var gate = new NotificationGate(clearMs: 100);
        gate.Observe(true, First, At(0));
        Assert.True(gate.Observe(true, First, At(100)));
        gate.Observe(false, null, At(200));
        gate.Observe(false, null, At(400));
        Assert.False(gate.Observe(true, First, At(500)));
        Assert.False(gate.Observe(true, First, At(600)));
        Assert.True(gate.Observe(true, First, At(1500)));
    }

    [Fact]
    public void LosingFocusDoesNotRecountTheSameVisibleNotification()
    {
        var gate = new NotificationGate();
        gate.Observe(true, First, At(0));
        Assert.True(gate.Observe(true, First, At(200)));
        gate.Observe(false, null, At(400));
        gate.BreakObservationSequence();
        Assert.False(gate.Observe(false, null, At(10000)));
        Assert.False(gate.Observe(true, First, At(10200)));
        Assert.False(gate.Observe(true, First, At(10400)));
    }

    [Fact]
    public void PausingDoesNotCarryOverHalfAConfirmation()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        gate.BreakObservationSequence();
        Assert.False(gate.Observe(true, First, At(10000)));
        Assert.True(gate.Observe(true, First, At(10200)));
    }

    [Fact]
    public void ManualCorrectionSuppressesTheCurrentNotificationUntilItClears()
    {
        var gate = new NotificationGate();
        gate.SuppressUntilClear();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.False(gate.Observe(true, First, At(200)));
        gate.Observe(false, null, At(400));
        gate.Observe(false, null, At(1400));
        Assert.False(gate.Observe(true, First, At(1600)));
        Assert.True(gate.Observe(true, First, At(1800)));
    }

    [Fact]
    public void JitterAcrossAGridLineKeepsTheSameSignature()
    {
        ChestNotification? steady = NotificationClassifier.Find(
            [Line("+1 200 ОП", y: 70), Line("СУНДУК", y: 95)], requireXp: true);
        ChestNotification? jittered = NotificationClassifier.Find(
            [Line("+1 200 ОП", y: 70), Line("СУНДУК", y: 96)], requireXp: true);
        Assert.NotNull(steady);
        Assert.NotNull(jittered);
        Assert.True(steady.Signature.Matches(jittered.Signature));
    }

    [Fact]
    public void RowShiftedNotificationHasADifferentSignature()
    {
        ChestNotification? first = NotificationClassifier.Find(
            [Line("+1 200 ОП", y: 70), Line("СУНДУК", y: 100)], requireXp: true);
        ChestNotification? shifted = NotificationClassifier.Find(
            [Line("+1 200 ОП", y: 94), Line("СУНДУК", y: 124)], requireXp: true);
        Assert.NotNull(first);
        Assert.NotNull(shifted);
        Assert.False(first.Signature.Matches(shifted.Signature));
    }

    [Fact]
    public void JitterAcrossAGridLineStillCountsASingleChest()
    {
        var gate = new NotificationGate();
        var low = new ChestSignature("СУНДУК", 10, 107.5);
        var high = new ChestSignature("СУНДУК", 10, 108.5);
        int count = Enumerable.Range(0, 10).Count(frame =>
            gate.Observe(true, frame % 2 == 0 ? low : high, At(frame * 200)));
        Assert.Equal(1, count);
    }

    [Fact]
    public void ContainsChestLabelSeesTheLabelWithoutAnAward()
    {
        Assert.True(NotificationClassifier.ContainsChestLabel([Line("СУНДУК")]));
        Assert.True(NotificationClassifier.ContainsChestLabel([Line("CHEST")]));
        Assert.False(NotificationClassifier.ContainsChestLabel([Line("+1 200 ОП", y: 70)]));
        Assert.False(NotificationClassifier.ContainsChestLabel([Line("ОБЫЩИТЕ СУНДУК")]));
    }

    [Fact]
    public void CountsConsecutiveNotificationsThatReplaceEachOther()
    {
        var gate = new NotificationGate();
        int count = 0;
        for (int frame = 0; frame < 20; frame++)
        {
            if (gate.Observe(true, First, At(frame * 200))) count++;
        }

        for (int frame = 20; frame < 30; frame++)
        {
            if (gate.Observe(true, Second, At(frame * 200))) count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountsANewNotificationSoonAfterThePreviousOne()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(true, Second, At(600)));
        Assert.False(gate.Observe(true, Second, At(800)));
        Assert.True(gate.Observe(true, Second, At(1000)));
    }

    [Fact]
    public void OneStrayDifferentReadingDoesNotCreateASecondCount()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(true, Second, At(400)));
        Assert.False(gate.Observe(true, First, At(600)));
        Assert.False(gate.Observe(true, Second, At(800)));
        Assert.False(gate.Observe(true, First, At(1000)));
    }

    [Fact]
    public void LabelWithoutAnAwardDoesNotRearmTheGate()
    {
        var gate = new NotificationGate();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.True(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(true, null, At(400)));
        Assert.False(gate.Observe(true, null, At(600)));
        Assert.False(gate.Observe(true, null, At(800)));
        Assert.False(gate.Observe(true, First, At(1000)));
    }

    [Fact]
    public void ManualCorrectionSuppressesADifferentReadingToo()
    {
        var gate = new NotificationGate();
        gate.SuppressUntilClear();
        Assert.False(gate.Observe(true, First, At(0)));
        Assert.False(gate.Observe(true, First, At(200)));
        Assert.False(gate.Observe(true, Second, At(400)));
        Assert.False(gate.Observe(true, Second, At(600)));
        Assert.False(gate.Observe(true, Second, At(800)));
        gate.Observe(false, null, At(1000));
        gate.Observe(false, null, At(1200));
        gate.Observe(false, null, At(1600));
        Assert.False(gate.Observe(true, First, At(1800)));
        Assert.True(gate.Observe(true, First, At(2000)));
    }

    private static readonly ChestSignature First = new("СУНДУК", 10, 10);
    private static readonly ChestSignature Second = new("СУНДУК", 10, 34);

    private static TimeSpan At(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
}