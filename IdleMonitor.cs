namespace MicSentry;

// Tracks system-wide idle time via GetLastInputInfo — the same counter Windows uses
// for screensaver/lock timing. It only reacts to real keyboard/mouse input events,
// never screen content, so it can't be fooled by something moving on screen while
// the user is actually away, and never observes what was pressed, only when.
internal sealed class IdleMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private bool _isIdle;

    public TimeSpan IdleThreshold { get; set; }

    public event EventHandler? IdleThresholdReached;
    public event EventHandler? ActivityResumed;

    public IdleMonitor(TimeSpan idleThreshold, TimeSpan pollInterval)
    {
        IdleThreshold = idleThreshold;
        _timer = new System.Windows.Forms.Timer { Interval = (int)pollInterval.TotalMilliseconds };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        _isIdle = false;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _isIdle = false;
    }

    public static TimeSpan GetIdleTime()
    {
        var lii = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };

        if (!NativeMethods.GetLastInputInfo(ref lii))
            return TimeSpan.Zero;

        uint now = unchecked((uint)Environment.TickCount);
        uint idleTicks = now - lii.dwTime; // unsigned subtraction wraps correctly across TickCount rollover
        return TimeSpan.FromMilliseconds(idleTicks);
    }

    private void Poll()
    {
        var idleFor = GetIdleTime();

        if (!_isIdle && idleFor >= IdleThreshold)
        {
            _isIdle = true;
            IdleThresholdReached?.Invoke(this, EventArgs.Empty);
        }
        else if (_isIdle && idleFor < IdleThreshold)
        {
            _isIdle = false;
            ActivityResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _timer.Dispose();
}
