namespace BreakWindow.Core;

public sealed class BreakTracker
{
    private static readonly TimeSpan ClockInCutoff = TimeSpan.FromHours(4);

    private readonly IClock clock;
    private readonly IIdleSource idle;
    private Settings settings;
    private Schedule schedule;

    private ClockOverride clockOverride;
    private DateTime overrideUntil;
    private bool active;
    private bool locked;
    private bool suspended;
    private DateTimeOffset lastTick;
    private DateTimeOffset lastInput;
    private TimeSpan work;
    private DateTimeOffset? dueSince;
    private DateTimeOffset? dueBeforeBreak;
    private DateTimeOffset? breakEnds;

    public BreakTracker(Settings settings, IClock clock, IIdleSource idle)
    {
        this.clock = clock;
        this.idle = idle;
        this.settings = settings;
        schedule = new Schedule(settings.Schedule);
        var now = clock.Now;
        lastTick = now;
        lastInput = now - idle.IdleTime;
        active = IsActiveAt(now);
    }

    private enum ClockOverride
    {
        None,
        ClockedIn,
        ClockedOut,
    }

    public Settings Settings => settings;

    public bool IsActive => active;

    public Phase Phase => !active ? Phase.Inactive
        : breakEnds is not null ? Phase.OnBreak
        : dueSince is not null ? Phase.BreakDue
        : Phase.Working;

    public TimeSpan WorkTime => work;

    public TimeSpan TimeUntilBreak => Max(settings.WorkLength - work, TimeSpan.Zero);

    public TimeSpan Overdue => dueSince is { } since ? Max(clock.Now - since, TimeSpan.Zero) : TimeSpan.Zero;

    public TimeSpan BreakRemaining => breakEnds is { } ends ? Max(ends - clock.Now, TimeSpan.Zero) : TimeSpan.Zero;

    public DateTime? InactiveUntil
    {
        get
        {
            if (active)
            {
                return null;
            }

            if (clockOverride == ClockOverride.ClockedOut)
            {
                return overrideUntil == DateTime.MaxValue ? null : overrideUntil;
            }

            return schedule.NextStart(clock.Now.DateTime);
        }
    }

    public void UpdateSettings(Settings newSettings)
    {
        settings = newSettings;
        schedule = new Schedule(newSettings.Schedule);
        Tick();
    }

    public void Tick()
    {
        var now = clock.Now;
        var elapsed = Max(now - lastTick, TimeSpan.Zero);
        lastTick = now;

        var gap = TrackInput(now);

        var nowActive = IsActiveAt(now);
        if (!nowActive)
        {
            if (active)
            {
                ResetWork();
            }

            active = false;
            return;
        }

        active = true;
        var idleTime = now - lastInput;
        if (gap >= settings.BreakLength || idleTime >= settings.BreakLength)
        {
            ResetWork();
        }

        if (breakEnds is { } ends)
        {
            if (now >= ends)
            {
                ResetWork();
            }

            return;
        }

        if (idleTime < settings.IdleGrace)
        {
            work += Min(elapsed, settings.IdleGrace);
        }

        if (dueSince is null && work >= settings.WorkLength)
        {
            dueSince = now;
        }
    }

    public void SetLocked(bool isLocked)
    {
        Tick();
        locked = isLocked;
        Tick();
    }

    public void SetSuspended(bool isSuspended)
    {
        Tick();
        suspended = isSuspended;
        Tick();
    }

    public void ClockIn()
    {
        Tick();
        if (active)
        {
            return;
        }

        var now = clock.Now.DateTime;
        clockOverride = ClockOverride.None;
        if (!schedule.Contains(now))
        {
            clockOverride = ClockOverride.ClockedIn;
            overrideUntil = NextCutoff(now);
        }

        Tick();
    }

    public void ClockOut()
    {
        Tick();
        if (!active)
        {
            return;
        }

        var now = clock.Now.DateTime;
        clockOverride = ClockOverride.None;
        if (schedule.Contains(now))
        {
            clockOverride = ClockOverride.ClockedOut;
            overrideUntil = schedule.NextStart(now) ?? DateTime.MaxValue;
        }

        Tick();
    }

    public void StartBreak()
    {
        Tick();
        if (!active || breakEnds is not null)
        {
            return;
        }

        dueBeforeBreak = dueSince;
        dueSince = null;
        breakEnds = clock.Now + settings.BreakLength;
    }

    public void CancelBreak()
    {
        if (breakEnds is null)
        {
            return;
        }

        breakEnds = null;
        dueSince = dueBeforeBreak;
        dueBeforeBreak = null;
        Tick();
    }

    private TimeSpan TrackInput(DateTimeOffset now)
    {
        if (locked || suspended)
        {
            return TimeSpan.Zero;
        }

        var reported = now - idle.IdleTime;
        if (reported <= lastInput)
        {
            return TimeSpan.Zero;
        }

        var gap = reported - lastInput;
        lastInput = reported;
        return gap;
    }

    private bool IsActiveAt(DateTimeOffset now)
    {
        var local = now.DateTime;
        if (clockOverride != ClockOverride.None && local >= overrideUntil)
        {
            clockOverride = ClockOverride.None;
        }

        return clockOverride switch
        {
            ClockOverride.ClockedIn => true,
            ClockOverride.ClockedOut => false,
            _ => schedule.Contains(local),
        };
    }

    private void ResetWork()
    {
        work = TimeSpan.Zero;
        dueSince = null;
        dueBeforeBreak = null;
        breakEnds = null;
    }

    private static DateTime NextCutoff(DateTime now)
    {
        var cutoff = now.Date + ClockInCutoff;
        return cutoff > now ? cutoff : cutoff.AddDays(1);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
