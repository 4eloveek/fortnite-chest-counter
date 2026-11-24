using FortniteChestCounter.Core;
using Xunit;

namespace FortniteChestCounter.Tests;

public sealed class DailyStatisticsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "opencode", "fortnite-daily-tests-" + Guid.NewGuid());
    private string StatePath => Path.Combine(_directory, "state.json");

    public DailyStatisticsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void EmptyHistoryIncludesZeroDaysWithoutCreatingAStateFile()
    {
        var ledger = new CounterLedger(StatePath);
        var first = new DateOnly(2026, 11, 23);
        Assert.Null(ledger.FirstRecordedDay);
        Assert.Equal([
            new DailyChestCount(first, 0),
            new DailyChestCount(first.AddDays(1), 0),
            new DailyChestCount(first.AddDays(2), 0)
        ], ledger.DailyCounts(first, first.AddDays(2)));
        Assert.False(File.Exists(StatePath));
    }

    [Fact]
    public void IncludesBothBoundariesAndExcludesEventsOutsideThePeriod()
    {
        var ledger = new CounterLedger(StatePath);
        var first = new DateOnly(2026, 11, 23);
        for (int i = -1; i <= 3; i++)
        {
            ledger.Add(LocalNoon(first.AddDays(i)), CountSource.Automatic);
        }

        Assert.Equal([
            new DailyChestCount(first, 1),
            new DailyChestCount(first.AddDays(1), 1),
            new DailyChestCount(first.AddDays(2), 1)
        ], ledger.DailyCounts(first, first.AddDays(2)));
    }

    [Fact]
    public void AggregatesAllMatchesAndAutomaticAndManualEntries()
    {
        var ledger = new CounterLedger(StatePath);
        var day = new DateOnly(2026, 11, 23);
        ledger.Add(LocalNoon(day), CountSource.Automatic);
        ledger.NewSession();
        ledger.Add(LocalNoon(day), CountSource.Manual);
        ledger.Add(LocalNoon(day), CountSource.Automatic);

        Assert.Equal([new DailyChestCount(day, 3)], ledger.DailyCounts(day, day));
        Assert.Equal(3, ledger.Snapshot(LocalNoon(day)).Today);
    }

    [Fact]
    public void SortsDaysAndFillsGapsEvenWhenEntriesAreNotChronological()
    {
        var ledger = new CounterLedger(StatePath);
        var first = new DateOnly(2026, 11, 23);
        ledger.Add(LocalNoon(first.AddDays(2)), CountSource.Manual);
        ledger.Add(LocalNoon(first), CountSource.Automatic);
        ledger.Add(LocalNoon(first.AddDays(2)), CountSource.Automatic);

        Assert.Equal(first, ledger.FirstRecordedDay);
        Assert.Equal([
            new DailyChestCount(first, 1),
            new DailyChestCount(first.AddDays(1), 0),
            new DailyChestCount(first.AddDays(2), 2)
        ], ledger.DailyCounts(first, first.AddDays(2)));
    }

    [Theory]
    [InlineData(2024, 2, 28, 2024, 3, 1, 3)]
    [InlineData(2026, 2, 28, 2026, 3, 1, 2)]
    [InlineData(2026, 12, 31, 2027, 1, 1, 2)]
    public void HandlesLeapDaysMonthAndYearBoundaries(int y1, int m1, int d1, int y2, int m2, int d2, int expectedDays)
    {
        var ledger = new CounterLedger(StatePath);
        var from = new DateOnly(y1, m1, d1);
        var through = new DateOnly(y2, m2, d2);
        IReadOnlyList<DailyChestCount> days = ledger.DailyCounts(from, through);
        Assert.Equal(expectedDays, days.Count);
        Assert.Equal(from, days[0].Day);
        Assert.Equal(through, days[^1].Day);
        Assert.All(days, day => Assert.Equal(0, day.Count));
    }

    [Fact]
    public void UndoChangesTheOriginalDayAndCanEmptyTheHistory()
    {
        var ledger = new CounterLedger(StatePath);
        var first = new DateOnly(2026, 11, 23);
        ledger.Add(LocalNoon(first), CountSource.Automatic);
        ledger.Add(LocalNoon(first.AddDays(1)), CountSource.Manual);
        Assert.True(ledger.UndoLastInSession());
        Assert.Equal([
            new DailyChestCount(first, 1),
            new DailyChestCount(first.AddDays(1), 0)
        ], ledger.DailyCounts(first, first.AddDays(1)));
        Assert.True(ledger.UndoLastInSession());
        Assert.Null(ledger.FirstRecordedDay);
        Assert.All(ledger.DailyCounts(first, first.AddDays(1)), day => Assert.Equal(0, day.Count));
    }

    [Fact]
    public void UsesTheSavedDayInsteadOfRecalculatingItFromTheTimestamp()
    {
        var day = new DateOnly(2026, 11, 24);
        Guid session = Guid.NewGuid();
        JsonFile.Write(StatePath, new CounterState
        {
            CurrentSessionId = session,
            Entries = [new CountEntry(Guid.NewGuid(), session,
                new DateTimeOffset(2026, 11, 22, 23, 30, 0, TimeSpan.Zero), "2026-11-24", CountSource.Automatic)]
        });
        var ledger = new CounterLedger(StatePath);
        Assert.Equal(day, ledger.FirstRecordedDay);
        Assert.Equal([new DailyChestCount(day, 1)], ledger.DailyCounts(day, day));
        Assert.Equal([new DailyChestCount(day.AddDays(-2), 0)], ledger.DailyCounts(day.AddDays(-2), day.AddDays(-2)));
    }

    [Fact]
    public void RestoresDailyHistoryAfterRestart()
    {
        var ledger = new CounterLedger(StatePath);
        var day = new DateOnly(2026, 11, 23);
        ledger.Add(LocalNoon(day), CountSource.Automatic);
        ledger.Add(LocalNoon(day.AddDays(1)), CountSource.Manual);
        var restored = new CounterLedger(StatePath);
        Assert.Equal(ledger.FirstRecordedDay, restored.FirstRecordedDay);
        Assert.Equal(ledger.DailyCounts(day, day.AddDays(1)), restored.DailyCounts(day, day.AddDays(1)));
    }

    [Fact]
    public void ViewingDifferentPeriodsDoesNotWriteOrResetTheState()
    {
        var ledger = new CounterLedger(StatePath);
        var day = new DateOnly(2026, 11, 23);
        ledger.Add(LocalNoon(day), CountSource.Manual);
        string original = File.ReadAllText(StatePath);
        CounterSnapshot snapshot = ledger.Snapshot(LocalNoon(day));
        _ = ledger.FirstRecordedDay;
        _ = ledger.DailyCounts(day.AddDays(-30), day.AddDays(30));
        _ = ledger.DailyCounts(day.AddDays(1), day.AddDays(1));
        Assert.Equal(original, File.ReadAllText(StatePath));
        Assert.Equal(snapshot, ledger.Snapshot(LocalNoon(day)));
    }

    [Fact]
    public void RejectsAnInvertedPeriodWithoutWritingAFile()
    {
        var ledger = new CounterLedger(StatePath);
        var day = new DateOnly(2026, 11, 23);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.DailyCounts(day, day.AddDays(-1)));
        Assert.False(File.Exists(StatePath));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void SingleDayAtDateOnlyLimitsDoesNotOverflow(int year, int month, int day)
    {
        var ledger = new CounterLedger(StatePath);
        var date = new DateOnly(year, month, day);
        Assert.Equal([new DailyChestCount(date, 0)], ledger.DailyCounts(date, date));
    }

    private static DateTimeOffset LocalNoon(DateOnly day) =>
        new(DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local));

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
