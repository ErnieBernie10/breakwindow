using BreakWindow.Core;

namespace BreakWindow.Core.Tests;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; set; } = now;
}

internal sealed class FakeInput(FakeClock clock) : IIdleSource
{
    public DateTimeOffset LastInput { get; set; } = clock.Now;

    public TimeSpan IdleTime => clock.Now - LastInput;
}

internal sealed class Harness
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    public Harness(DateTime start, Settings? settings = null)
    {
        Clock = new FakeClock(new DateTimeOffset(start, TimeSpan.FromHours(1)));
        Input = new FakeInput(Clock);
        Tracker = new BreakTracker(settings ?? new Settings(), Clock, Input);
    }

    public FakeClock Clock { get; }

    public FakeInput Input { get; }

    public BreakTracker Tracker { get; }

    public static DateTime Monday(int hour, int minute = 0) => new(2026, 11, 23, hour, minute, 0);

    public void Work(TimeSpan duration)
    {
        var end = Clock.Now + duration;
        while (Clock.Now < end)
        {
            Clock.Now += TickInterval;
            Input.LastInput = Clock.Now;
            Tracker.Tick();
        }
    }

    public void Idle(TimeSpan duration)
    {
        var end = Clock.Now + duration;
        while (Clock.Now < end)
        {
            Clock.Now += TickInterval;
            Tracker.Tick();
        }
    }

    public void WorkMinutes(double minutes) => Work(TimeSpan.FromMinutes(minutes));

    public void IdleMinutes(double minutes) => Idle(TimeSpan.FromMinutes(minutes));
}
