using System.Globalization;

namespace FortniteChestCounter.Core;

public enum CountSource
{
    Automatic,
    Manual
}

public sealed record CountEntry(
    Guid Id,
    Guid SessionId,
    DateTimeOffset Timestamp,
    string Day,
    CountSource Source);

public sealed record CounterState
{
    public int Version { get; init; } = 1;
    public Guid CurrentSessionId { get; init; } = Guid.NewGuid();
    public List<CountEntry> Entries { get; init; } = [];
}

public readonly record struct CounterSnapshot(int Session, int Today, int Total);

public readonly record struct DailyChestCount(DateOnly Day, int Count);

public sealed class CounterLedger
{
    private readonly string _path;
    private CounterState _state;

    public CounterLedger(string path)
    {
        _path = path;
        _state = JsonFile.Read<CounterState>(path) ?? new CounterState();
        Validate(_state);
    }

    public CounterSnapshot Snapshot(DateTimeOffset now)
    {
        string today = DayKey(now);
        return new CounterSnapshot(
            _state.Entries.Count(entry => entry.SessionId == _state.CurrentSessionId),
            _state.Entries.Count(entry => entry.Day == today),
            _state.Entries.Count);
    }

    public DateOnly? FirstRecordedDay => _state.Entries.Count == 0
        ? null
        : DateOnly.ParseExact(_state.Entries.Min(entry => entry.Day)!, "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

    public IReadOnlyList<DailyChestCount> DailyCounts(DateOnly from, DateOnly through)
    {
        if (through < from)
        {
            throw new ArgumentOutOfRangeException(nameof(through), "Конец периода не может быть раньше начала.");
        }

        // Use the saved local day, not a timestamp converted in today's time zone.
        // All sessions and both count sources contribute; undone entries are already removed.
        Dictionary<string, int> counts = _state.Entries.GroupBy(entry => entry.Day)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var days = new DailyChestCount[through.DayNumber - from.DayNumber + 1];
        for (int i = 0; i < days.Length; i++)
        {
            DateOnly day = DateOnly.FromDayNumber(from.DayNumber + i);
            counts.TryGetValue(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out int count);
            days[i] = new DailyChestCount(day, count);
        }

        return days;
    }

    public void Add(DateTimeOffset now, CountSource source)
    {
        var entry = new CountEntry(Guid.NewGuid(), _state.CurrentSessionId, now, DayKey(now), source);
        Commit(_state with { Entries = [.. _state.Entries, entry] });
    }

    public bool UndoLastInSession()
    {
        int index = _state.Entries.FindLastIndex(entry => entry.SessionId == _state.CurrentSessionId);
        if (index < 0)
        {
            return false;
        }

        List<CountEntry> entries = [.. _state.Entries];
        entries.RemoveAt(index);
        Commit(_state with { Entries = entries });
        return true;
    }

    public void NewSession()
    {
        Commit(_state with { CurrentSessionId = Guid.NewGuid() });
    }

    private void Commit(CounterState next)
    {
        // Update the in-memory counters only after the complete state is safely written.
        JsonFile.Write(_path, next);
        _state = next;
    }

    private static string DayKey(DateTimeOffset time) =>
        time.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void Validate(CounterState state)
    {
        if (state.Version != 1 || state.CurrentSessionId == Guid.Empty || state.Entries is null)
        {
            throw new InvalidDataException("Некорректный или неподдерживаемый файл статистики.");
        }

        var ids = new HashSet<Guid>();
        foreach (CountEntry entry in state.Entries)
        {
            if (entry is null || entry.Id == Guid.Empty || entry.SessionId == Guid.Empty ||
                !ids.Add(entry.Id) || !Enum.IsDefined(entry.Source) ||
                !DateOnly.TryParseExact(entry.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
            {
                throw new InvalidDataException("Повреждена запись в файле статистики.");
            }
        }
    }
}
