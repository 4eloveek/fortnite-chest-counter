using FortniteChestCounter.Core;
using Xunit;

namespace FortniteChestCounter.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "opencode", "fortnite-tests-" + Guid.NewGuid());
    private string StatePath => Path.Combine(_directory, "state.json");

    public StorageTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void RestoresCountersAndSessionAfterRestart()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        var first = new CounterLedger(StatePath);
        first.Add(now, CountSource.Automatic);
        first.Add(now, CountSource.Manual);
        var restored = new CounterLedger(StatePath);
        Assert.Equal(new CounterSnapshot(2, 2, 2), restored.Snapshot(now));
    }

    [Fact]
    public void NewMatchResetsOnlySessionAndCannotUndoAnEarlierMatch()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        var ledger = new CounterLedger(StatePath);
        ledger.Add(now, CountSource.Automatic);
        ledger.NewSession();
        Assert.Equal(new CounterSnapshot(0, 1, 1), ledger.Snapshot(now));
        Assert.False(ledger.UndoLastInSession());
        ledger.Add(now, CountSource.Manual);
        Assert.True(ledger.UndoLastInSession());
        Assert.Equal(new CounterSnapshot(0, 1, 1), new CounterLedger(StatePath).Snapshot(now));
    }

    [Fact]
    public void DayRolloverAndUndoUseTheOriginalEventDate()
    {
        var beforeMidnight = new DateTimeOffset(new DateTime(2026, 11, 23, 23, 59, 0, DateTimeKind.Local));
        DateTimeOffset afterMidnight = beforeMidnight.AddMinutes(2);
        var ledger = new CounterLedger(StatePath);
        ledger.Add(beforeMidnight, CountSource.Automatic);
        Assert.Equal(new CounterSnapshot(1, 0, 1), ledger.Snapshot(afterMidnight));
        ledger.Add(afterMidnight, CountSource.Manual);
        Assert.Equal(new CounterSnapshot(2, 1, 2), ledger.Snapshot(afterMidnight));
        ledger.UndoLastInSession();
        Assert.Equal(new CounterSnapshot(1, 0, 1), ledger.Snapshot(afterMidnight));
        ledger.UndoLastInSession();
        Assert.Equal(new CounterSnapshot(0, 0, 0), ledger.Snapshot(beforeMidnight));
    }

    [Fact]
    public void FailedSaveDoesNotIncreaseTheDisplayedCounter()
    {
        // A directory occupying the target filename makes the final atomic replacement fail.
        Directory.CreateDirectory(StatePath);
        var ledger = new CounterLedger(StatePath);
        Exception? failure = Record.Exception(() => ledger.Add(DateTimeOffset.Now, CountSource.Automatic));
        Assert.NotNull(failure);
        // Windows can report access denied for a directory target; Unix reports an I/O error.
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Equal(new CounterSnapshot(0, 0, 0), ledger.Snapshot(DateTimeOffset.Now));
    }

    [Fact]
    public void CorruptStateIsPreservedInsteadOfSilentlyOverwritten()
    {
        const string broken = "{ interrupted write";
        File.WriteAllText(StatePath, broken);
        Assert.Throws<InvalidDataException>(() => new CounterLedger(StatePath));
        Assert.Equal(broken, File.ReadAllText(StatePath));
    }

    [Fact]
    public void RejectsAnUnknownStateVersion()
    {
        JsonFile.Write(StatePath, new CounterState { Version = 99 });
        Assert.Throws<InvalidDataException>(() => new CounterLedger(StatePath));
    }

    [Fact]
    public void SettingsRoundTripPreservesNegativeMonitorCoordinates()
    {
        string path = Path.Combine(_directory, "settings.json");
        var settings = new AppSettings
        {
            Area = new CaptureArea(-1650, 200, 420, 140), Language = "eng",
            OverlayCorner = OverlayCorner.BottomRight, BrightTextMask = false
        };
        JsonFile.Write(path, settings);
        AppSettings restored = JsonFile.Read<AppSettings>(path)!;
        restored.Validate();
        Assert.Equal(settings, restored);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
