using BreakWindow.Core;

namespace BreakWindow.Core.Tests;

public class BreakTrackerTests
{
    private static readonly Settings Defaults = new();

    [Fact]
    public void Break_is_due_after_work_minutes_of_work()
    {
        var h = new Harness(Harness.Monday(9));

        h.WorkMinutes(54.5);
        Assert.Equal(Phase.Working, h.Tracker.Phase);

        h.WorkMinutes(0.5);
        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
    }

    [Fact]
    public void Idle_shorter_than_the_grace_counts_as_work()
    {
        var h = new Harness(Harness.Monday(9));

        h.WorkMinutes(10);
        h.Idle(TimeSpan.FromSeconds(50));

        Assert.Equal(TimeSpan.FromSeconds(650), h.Tracker.WorkTime);
    }

    [Fact]
    public void Idle_beyond_the_grace_does_not_count()
    {
        var h = new Harness(Harness.Monday(9));

        h.WorkMinutes(10);
        h.IdleMinutes(4);

        Assert.True(h.Tracker.WorkTime < TimeSpan.FromMinutes(11));
        Assert.Equal(Phase.Working, h.Tracker.Phase);
    }

    [Fact]
    public void Idle_for_the_break_length_counts_as_a_break()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.IdleMinutes(5);

        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Idle_for_the_break_length_closes_the_reminder()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(56);
        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);

        h.IdleMinutes(5);

        Assert.Equal(Phase.Working, h.Tracker.Phase);
        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Reminder_stays_while_working_on()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(55);

        h.WorkMinutes(12);

        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
        Assert.Equal("12 min overdue", StatusText.Overdue(h.Tracker.Overdue));
    }

    [Fact]
    public void Short_idle_does_not_close_the_reminder()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(55);

        h.IdleMinutes(4);

        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
    }

    [Fact]
    public void Locked_pc_counts_as_idle_even_if_input_is_reported()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.Tracker.SetLocked(true);
        h.WorkMinutes(5);

        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Short_lock_does_not_count_as_work_or_break()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.Tracker.SetLocked(true);
        h.WorkMinutes(3);
        h.Tracker.SetLocked(false);
        h.WorkMinutes(1);

        Assert.InRange(h.Tracker.WorkTime, TimeSpan.FromMinutes(31), TimeSpan.FromMinutes(32.1));
    }

    [Fact]
    public void Sleep_counts_toward_idle_after_resume()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.Tracker.SetSuspended(true);
        h.Clock.Now += TimeSpan.FromMinutes(20);
        h.Input.LastInput = h.Clock.Now;
        h.Tracker.SetSuspended(false);

        Assert.True(h.Tracker.WorkTime <= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Sleep_without_a_notification_still_counts_as_idle()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.Clock.Now += TimeSpan.FromMinutes(20);
        h.Input.LastInput = h.Clock.Now;
        h.Tracker.Tick();

        Assert.True(h.Tracker.WorkTime <= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Short_sleep_does_not_count_as_work()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(30);

        h.Tracker.SetSuspended(true);
        h.Clock.Now += TimeSpan.FromMinutes(3);
        h.Input.LastInput = h.Clock.Now;
        h.Tracker.SetSuspended(false);

        Assert.InRange(h.Tracker.WorkTime, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(31));
    }

    [Fact]
    public void Finished_break_resets_work_time()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(56);

        h.Tracker.StartBreak();
        Assert.Equal(Phase.OnBreak, h.Tracker.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), h.Tracker.BreakRemaining);

        h.WorkMinutes(5);

        Assert.Equal(Phase.Working, h.Tracker.Phase);
        Assert.True(h.Tracker.WorkTime <= Harness.TickInterval);
    }

    [Fact]
    public void Work_does_not_count_during_a_break()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(20);
        h.Tracker.StartBreak();

        h.WorkMinutes(3);

        Assert.Equal(Phase.OnBreak, h.Tracker.Phase);
        Assert.Equal(TimeSpan.FromMinutes(20), h.Tracker.WorkTime);
    }

    [Fact]
    public void Cancelled_break_brings_the_reminder_back()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(55);
        h.WorkMinutes(3);

        h.Tracker.StartBreak();
        h.WorkMinutes(1);
        h.Tracker.CancelBreak();

        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
        Assert.Equal("4 min overdue", StatusText.Overdue(h.Tracker.Overdue));
    }

    [Fact]
    public void Cancelled_early_break_returns_to_work_with_work_time_kept()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(20);

        h.Tracker.StartBreak();
        h.WorkMinutes(1);
        h.Tracker.CancelBreak();

        Assert.Equal(Phase.Working, h.Tracker.Phase);
        Assert.InRange(h.Tracker.WorkTime, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(20.1));
    }

    [Fact]
    public void Nothing_counts_outside_the_schedule()
    {
        var h = new Harness(Harness.Monday(17));

        h.WorkMinutes(90);

        Assert.Equal(Phase.Inactive, h.Tracker.Phase);
        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Reminder_closes_when_the_schedule_ends()
    {
        var h = new Harness(Harness.Monday(15, 30));
        h.WorkMinutes(56);
        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);

        h.WorkMinutes(5);

        Assert.Equal(Phase.Inactive, h.Tracker.Phase);
        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Clock_in_outside_the_schedule_lasts_until_four_the_next_day()
    {
        var h = new Harness(Harness.Monday(20));

        h.Tracker.ClockIn();
        Assert.True(h.Tracker.IsActive);

        h.IdleMinutes(7 * 60 + 55);
        Assert.True(h.Tracker.IsActive);

        h.IdleMinutes(5);
        Assert.False(h.Tracker.IsActive);
    }

    [Fact]
    public void Clocked_in_work_counts_toward_a_break()
    {
        var h = new Harness(Harness.Monday(20));
        h.Tracker.ClockIn();

        h.WorkMinutes(55);

        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
    }

    [Fact]
    public void Clock_out_after_clocking_in_ends_activity()
    {
        var h = new Harness(Harness.Monday(20));
        h.Tracker.ClockIn();
        h.WorkMinutes(56);

        h.Tracker.ClockOut();

        Assert.Equal(Phase.Inactive, h.Tracker.Phase);
        Assert.Equal(TimeSpan.Zero, h.Tracker.WorkTime);
    }

    [Fact]
    public void Clock_out_during_the_schedule_lasts_until_the_next_start()
    {
        var h = new Harness(Harness.Monday(10));

        h.Tracker.ClockOut();
        Assert.Equal(Phase.Inactive, h.Tracker.Phase);
        Assert.Equal(Harness.Monday(8).AddDays(1), h.Tracker.InactiveUntil);
        Assert.Equal("Off until Tue 08:00", StatusText.Tooltip(h.Tracker));

        h.IdleMinutes(21 * 60 + 55);
        Assert.False(h.Tracker.IsActive);

        h.IdleMinutes(5);
        Assert.True(h.Tracker.IsActive);
    }

    [Fact]
    public void Clock_in_after_clocking_out_resumes_the_schedule()
    {
        var h = new Harness(Harness.Monday(10));
        h.Tracker.ClockOut();

        h.Tracker.ClockIn();

        Assert.True(h.Tracker.IsActive);
        h.IdleMinutes(7);
        Assert.True(h.Tracker.IsActive);
    }

    [Fact]
    public void Shorter_work_minutes_from_new_settings_make_the_break_due()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(10);

        h.Tracker.UpdateSettings(Defaults with { WorkMinutes = 5 });

        Assert.Equal(Phase.BreakDue, h.Tracker.Phase);
    }

    [Fact]
    public void Start_break_is_ignored_while_inactive()
    {
        var h = new Harness(Harness.Monday(18));

        h.Tracker.StartBreak();

        Assert.Equal(Phase.Inactive, h.Tracker.Phase);
    }

    [Fact]
    public void Tooltip_counts_down_to_the_next_break()
    {
        var h = new Harness(Harness.Monday(9));

        h.WorkMinutes(32);

        Assert.Equal("Next break in 23 min", StatusText.Tooltip(h.Tracker));
    }

    [Fact]
    public void Tooltip_names_each_phase()
    {
        var h = new Harness(Harness.Monday(9));
        h.WorkMinutes(55);
        Assert.Equal("Break due", StatusText.Tooltip(h.Tracker));

        h.Tracker.StartBreak();
        Assert.Equal("On break", StatusText.Tooltip(h.Tracker));
    }

    [Fact]
    public void Tooltip_after_friday_names_monday()
    {
        var h = new Harness(new DateTime(2026, 11, 27, 18, 0, 0));

        Assert.Equal("Off until Mon 08:00", StatusText.Tooltip(h.Tracker));
    }

    [Theory]
    [InlineData(0, "Due now")]
    [InlineData(59, "Due now")]
    [InlineData(60, "1 min overdue")]
    [InlineData(725, "12 min overdue")]
    public void Overdue_text_is_in_whole_minutes(int seconds, string expected)
    {
        Assert.Equal(expected, StatusText.Overdue(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(300, "5:00")]
    [InlineData(299.2, "5:00")]
    [InlineData(61, "1:01")]
    [InlineData(0, "0:00")]
    public void Countdown_rounds_up_to_whole_seconds(double seconds, string expected)
    {
        Assert.Equal(expected, StatusText.Countdown(TimeSpan.FromSeconds(seconds)));
    }
}
