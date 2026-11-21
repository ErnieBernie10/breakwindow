namespace BreakWindow.Core;

public sealed record Settings
{
    public IReadOnlyList<ScheduleRange> Schedule { get; set; } =
    [
        new ScheduleRange
        {
            Days = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 30),
        },
    ];

    public int WorkMinutes { get; set; } = 55;

    public int BreakMinutes { get; set; } = 5;

    public int IdleGraceSeconds { get; set; } = 60;

    public bool StartWithWindows { get; set; } = true;

    public TimeSpan WorkLength => TimeSpan.FromMinutes(WorkMinutes);

    public TimeSpan BreakLength => TimeSpan.FromMinutes(BreakMinutes);

    public TimeSpan IdleGrace => TimeSpan.FromSeconds(IdleGraceSeconds);
}

public sealed record ScheduleRange
{
    public IReadOnlyList<DayOfWeek> Days { get; set; } = [];

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }
}
