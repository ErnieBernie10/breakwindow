using System.Globalization;

namespace BreakWindow.Core;

public static class StatusText
{
    public static string Tooltip(BreakTracker tracker) => tracker.Phase switch
    {
        Phase.Working => $"Next break in {CeilMinutes(tracker.TimeUntilBreak)} min",
        Phase.BreakDue => "Break due",
        Phase.OnBreak => "On break",
        _ => tracker.InactiveUntil is { } until
            ? "Off until " + until.ToString("ddd HH:mm", CultureInfo.InvariantCulture)
            : "Off",
    };

    public static string Overdue(TimeSpan overdue)
    {
        var minutes = (int)overdue.TotalMinutes;
        return minutes < 1 ? "Due now" : $"{minutes} min overdue";
    }

    public static string Countdown(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static int CeilMinutes(TimeSpan time) => Math.Max(1, (int)Math.Ceiling(time.TotalMinutes));
}
