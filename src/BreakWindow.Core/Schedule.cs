namespace BreakWindow.Core;

public sealed class Schedule(IReadOnlyList<ScheduleRange> ranges)
{
    public bool Contains(DateTime time)
    {
        var timeOfDay = TimeOnly.FromDateTime(time);
        foreach (var range in ranges)
        {
            if (range.Days.Contains(time.DayOfWeek) && range.Start <= timeOfDay && timeOfDay < range.End)
            {
                return true;
            }
        }

        return false;
    }

    public DateTime? NextStart(DateTime after)
    {
        DateTime? next = null;
        for (var day = 0; day <= 7; day++)
        {
            var date = after.Date.AddDays(day);
            foreach (var range in ranges)
            {
                if (!range.Days.Contains(date.DayOfWeek))
                {
                    continue;
                }

                var start = date + range.Start.ToTimeSpan();
                if (start > after && (next is null || start < next))
                {
                    next = start;
                }
            }

            if (next is not null)
            {
                return next;
            }
        }

        return next;
    }
}
