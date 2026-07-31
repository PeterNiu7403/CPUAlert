using System.Runtime.InteropServices;

namespace WinMoe.Services;

/// <summary>
/// Mole "保持常亮" — keeps the display (and system) awake for a fixed window or
/// indefinitely via SetThreadExecutionState. Read-only, reversible, no admin needed.
/// </summary>
public interface IDisplaySleepPreventionService
{
    bool IsActive { get; }

    /// <summary>Time left before auto-release; null when indefinite or inactive.</summary>
    TimeSpan? Remaining { get; }

    /// <summary>Active duration chosen by the user (null = indefinite) — for menu check marks.</summary>
    TimeSpan? ActiveDuration { get; }

    /// <summary>Arm keep-awake. duration null means "不限时" (until stopped).</summary>
    void PreventFor(TimeSpan? duration);

    void Stop();
}

public sealed class DisplaySleepPreventionService : IDisplaySleepPreventionService, IDisposable
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    private readonly object _gate = new();
    private Timer? _expiryTimer;
    private DateTimeOffset? _expiresAt;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _expiresAt is not null || _isIndefinite;
            }
        }
    }

    public TimeSpan? Remaining
    {
        get
        {
            lock (_gate)
            {
                return _expiresAt is { } expires
                    ? expires - DateTimeOffset.Now
                    : null;
            }
        }
    }

    public TimeSpan? ActiveDuration
    {
        get
        {
            lock (_gate)
            {
                return _isIndefinite ? null : _activeDuration;
            }
        }
    }

    private bool _isIndefinite;
    private TimeSpan? _activeDuration;

    public void PreventFor(TimeSpan? duration)
    {
        lock (_gate)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;
            _isIndefinite = duration is null;
            _activeDuration = duration;

            // System + display stay awake; the request persists until cleared.
            SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);

            if (duration is { } window)
            {
                _expiresAt = DateTimeOffset.Now + window;
                _expiryTimer = new Timer(_ => Stop(), null, window, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _expiresAt = null;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _expiryTimer?.Dispose();
            _expiryTimer = null;
            _expiresAt = null;
            _isIndefinite = false;
            _activeDuration = null;

            // ES_CONTINUOUS alone clears the previously requested flags.
            SetThreadExecutionState(EsContinuous);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    /// <summary>Tray menu label for the remaining time, e.g. "剩余 3:59"; null when not timed.</summary>
    internal static string? FormatRemaining(TimeSpan? remaining)
    {
        if (remaining is not { } value || value <= TimeSpan.Zero)
        {
            return null;
        }

        return value.TotalHours >= 1
            ? $"剩余 {(int)value.TotalHours}:{value.Minutes:00}"
            : $"剩余 {Math.Max(1, value.Minutes)} 分钟";
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);
}
