using BreakWindow.Core;

namespace BreakWindow.Core.Tests;

public class ScheduleTests
{
    private readonly Schedule schedule = new(new Settings().Schedule);

    [Theory]
    [InlineData(2026, 11, 23, 8, 0, true)]
    [InlineData(2026, 11, 23, 12, 0, true)]
    [InlineData(2026, 11, 27, 16, 29, true)]
    [InlineData(2026, 11, 23, 7, 59, false)]
    [InlineData(2026, 11, 23, 16, 30, false)]
    [InlineData(2026, 11, 28, 12, 0, false)]
    [InlineData(2026, 11, 29, 12, 0, false)]
    public void Default_schedule_is_weekdays_from_eight_to_half_past_four(int y, int mo, int d, int h, int mi, bool expected)
    {
        Assert.Equal(expected, schedule.Contains(new DateTime(y, mo, d, h, mi, 0)));
    }

    [Fact]
    public void Next_start_after_friday_evening_is_monday_morning()
    {
        Assert.Equal(new DateTime(2026, 11, 30, 8, 0, 0), schedule.NextStart(new DateTime(2026, 11, 27, 17, 0, 0)));
    }

    [Fact]
    public void Next_start_during_a_range_is_the_next_day()
    {
        Assert.Equal(new DateTime(2026, 11, 24, 8, 0, 0), schedule.NextStart(new DateTime(2026, 11, 23, 9, 0, 0)));
    }

    [Fact]
    public void Next_start_before_a_range_is_the_same_day()
    {
        Assert.Equal(new DateTime(2026, 11, 23, 8, 0, 0), schedule.NextStart(new DateTime(2026, 11, 23, 6, 0, 0)));
    }

    [Fact]
    public void Empty_schedule_has_no_next_start()
    {
        Assert.Null(new Schedule([]).NextStart(new DateTime(2026, 11, 23, 6, 0, 0)));
    }
}
