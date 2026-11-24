namespace FortniteChestCounter.Core;

/// <summary>
/// Counts a notification once, requiring two observations and a sustained clear screen to rearm.
/// Only actual scans are observations: pausing or losing focus is not evidence that it disappeared.
/// </summary>
public sealed class NotificationGate
{
    private readonly int _confirmations;
    private readonly TimeSpan _clearTime;
    private readonly TimeSpan _minimumInterval;
    private int _hits;
    private bool _latched;
    private TimeSpan? _clearSince;
    private TimeSpan? _lastCount;
    private TimeSpan? _lastObservation;

    public NotificationGate(int confirmations = 2, int clearMs = 900, int minimumIntervalMs = 1400)
    {
        if (confirmations < 1 || clearMs < 0 || minimumIntervalMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmations));
        }

        _confirmations = confirmations;
        _clearTime = TimeSpan.FromMilliseconds(clearMs);
        _minimumInterval = TimeSpan.FromMilliseconds(minimumIntervalMs);
    }

    public bool Observe(bool present, TimeSpan now)
    {
        if (_lastObservation is { } previous && now < previous)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Use a monotonic clock.");
        }

        _lastObservation = now;

        if (!present)
        {
            _hits = 0;
            _clearSince ??= now;
            if (now - _clearSince.Value >= _clearTime)
            {
                _latched = false;
            }

            return false;
        }

        _clearSince = null;
        if (_latched)
        {
            return false;
        }

        _hits = Math.Min(_hits + 1, _confirmations);
        if (_hits < _confirmations ||
            (_lastCount is { } lastCount && now - lastCount < _minimumInterval))
        {
            return false;
        }

        _latched = true;
        _hits = 0;
        _lastCount = now;
        return true;
    }

    public void SuppressUntilClear()
    {
        _latched = true;
        _hits = 0;
        _clearSince = null;
    }

    public void BreakObservationSequence()
    {
        // A gap must not accumulate confirmations or satisfy the clear-screen timer.
        _hits = 0;
        _clearSince = null;
    }
}
