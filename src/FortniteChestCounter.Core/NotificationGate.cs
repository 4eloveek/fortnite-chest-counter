namespace FortniteChestCounter.Core;

/// <summary>
/// Turns a stream of screen observations into chest-open events.
///
/// A notification is an episode: it starts when a stable chest message appears,
/// counts once, and can only be counted again after the screen has been free of
/// the chest message for a sustained period. A different message (text or HUD
/// position) is treated as a new notification even when the previous one has not
/// fully cleared yet, which is the normal case when several chests are opened in
/// a row. OCR text flicker alone must not create a second count.
///
/// Only actual scans are observations: pausing or losing focus is not evidence
/// that the notification disappeared.
/// </summary>
public sealed class NotificationGate
{
    private readonly int _confirmations;
    private readonly int _changeConfirmations;
    private readonly int _clearConfirmations;
    private readonly TimeSpan _clearTime;
    private readonly TimeSpan _minimumInterval;

    private int _hits;
    private string? _pendingSignature;
    private int _changeHits;
    private string? _changeSignature;
    private bool _latched;
    private bool _suppressed;
    private string? _latchedSignature;
    private int _clearHits;
    private TimeSpan? _clearSince;
    private TimeSpan? _lastCount;
    private TimeSpan? _lastObservation;

    public NotificationGate(
        int confirmations = 2,
        int changeConfirmations = 3,
        int clearMs = 200,
        int clearConfirmations = 2,
        int minimumIntervalMs = 700)
    {
        if (confirmations < 1 || changeConfirmations < 1 || clearConfirmations < 1 ||
            clearMs < 0 || minimumIntervalMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmations));
        }

        _confirmations = confirmations;
        _changeConfirmations = changeConfirmations;
        _clearConfirmations = clearConfirmations;
        _clearTime = TimeSpan.FromMilliseconds(clearMs);
        _minimumInterval = TimeSpan.FromMilliseconds(minimumIntervalMs);
    }

    /// <summary>
    /// Observes one scan. <paramref name="visible"/> says a chest message is still
    /// on screen (even if its award line was misread); <paramref name="signature"/>
    /// identifies a fully recognised notification, or is null when none is accepted.
    /// </summary>
    public bool Observe(bool visible, string? signature, TimeSpan now)
    {
        if (_lastObservation is { } previous && now < previous)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Use a monotonic clock.");
        }

        _lastObservation = now;
        visible |= signature is not null;

        if (!visible)
        {
            _hits = 0;
            _pendingSignature = null;
            _changeHits = 0;
            _changeSignature = null;
            _clearSince ??= now;
            _clearHits++;

            if (_clearHits >= _clearConfirmations && now - _clearSince.Value >= _clearTime)
            {
                _latched = false;
                _suppressed = false;
                _latchedSignature = null;
            }

            return false;
        }

        _clearHits = 0;
        _clearSince = null;

        if (signature is null)
        {
            // The chest label is visible but the award was not read this scan.
            // It is not evidence of disappearance, so the latch stays armed.
            _hits = 0;
            _pendingSignature = null;
            _changeHits = 0;
            _changeSignature = null;
            return false;
        }

        if (!_latched)
        {
            if (_pendingSignature == signature)
            {
                _hits++;
            }
            else
            {
                _pendingSignature = signature;
                _hits = 1;
            }

            if (_hits < _confirmations ||
                (_lastCount is { } lastCount && now - lastCount < _minimumInterval))
            {
                return false;
            }

            Count(signature, now);
            return true;
        }

        if (_suppressed)
        {
            return false;
        }

        if (signature == _latchedSignature)
        {
            _changeHits = 0;
            _changeSignature = null;
            return false;
        }

        if (_changeSignature == signature)
        {
            _changeHits++;
        }
        else
        {
            _changeSignature = signature;
            _changeHits = 1;
        }

        if (_changeHits < _changeConfirmations ||
            (_lastCount is { } previousCount && now - previousCount < _minimumInterval))
        {
            return false;
        }

        Count(signature, now);
        return true;
    }

    public void SuppressUntilClear()
    {
        _latched = true;
        _suppressed = true;
        _latchedSignature = null;
        _hits = 0;
        _pendingSignature = null;
        _changeHits = 0;
        _changeSignature = null;
        _clearHits = 0;
        _clearSince = null;
    }

    public void BreakObservationSequence()
    {
        // A gap must not accumulate confirmations or satisfy the clear-screen timer.
        _hits = 0;
        _pendingSignature = null;
        _changeHits = 0;
        _changeSignature = null;
        _clearHits = 0;
        _clearSince = null;
    }

    private void Count(string signature, TimeSpan now)
    {
        _latched = true;
        _suppressed = false;
        _latchedSignature = signature;
        _hits = 0;
        _pendingSignature = null;
        _changeHits = 0;
        _changeSignature = null;
        _lastCount = now;
    }
}