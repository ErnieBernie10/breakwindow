using BreakWindow.Core;

namespace BreakWindow.Core.Tests;

public class SettingsJsonTests
{
    [Fact]
    public void Defaults_round_trip()
    {
        var json = SettingsJson.Serialize(new Settings());

        Assert.True(SettingsJson.TryParse(json, out var parsed, out _));
        Assert.Equal(55, parsed.WorkMinutes);
        Assert.Equal(5, parsed.BreakMinutes);
        Assert.Equal(60, parsed.IdleGraceSeconds);
        Assert.True(parsed.StartWithWindows);
        var range = Assert.Single(parsed.Schedule);
        Assert.Equal(new TimeOnly(8, 0), range.Start);
        Assert.Equal(new TimeOnly(16, 30), range.End);
        Assert.Equal(5, range.Days.Count);
    }

    [Fact]
    public void Serialized_defaults_use_readable_names_and_times()
    {
        var json = SettingsJson.Serialize(new Settings());

        Assert.Contains("\"workMinutes\": 55", json);
        Assert.Contains("\"start\": \"08:00\"", json);
        Assert.Contains("\"Monday\"", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void Missing_fields_take_defaults()
    {
        Assert.True(SettingsJson.TryParse("""{ "workMinutes": 1 }""", out var parsed, out _));

        Assert.Equal(1, parsed.WorkMinutes);
        Assert.Equal(5, parsed.BreakMinutes);
        Assert.Single(parsed.Schedule);
    }

    [Fact]
    public void Accepts_lowercase_days_short_times_comments_and_trailing_commas()
    {
        const string json = """
            {
              // weekend shift
              "schedule": [ { "days": ["saturday"], "start": "9:00", "end": "12:15", } ],
            }
            """;

        Assert.True(SettingsJson.TryParse(json, out var parsed, out var error), error);
        var range = Assert.Single(parsed.Schedule);
        Assert.Equal([DayOfWeek.Saturday], range.Days);
        Assert.Equal(new TimeOnly(9, 0), range.Start);
        Assert.Equal(new TimeOnly(12, 15), range.End);
    }

    [Fact]
    public void Syntax_error_reports_the_line()
    {
        Assert.False(SettingsJson.TryParse("{\n  \"workMinutes\": 55,\n  \"breakMinutes\": oops\n}", out _, out var error));
        Assert.Equal("error on line 3", error);
    }

    [Theory]
    [InlineData("""{ "workMinutes": 0 }""", "workMinutes must be 1 to 1440")]
    [InlineData("""{ "breakMinutes": -1 }""", "breakMinutes must be 1 to 1440")]
    [InlineData("""{ "idleGraceSeconds": 0 }""", "idleGraceSeconds must be 1 to 86400")]
    [InlineData("""{ "schedule": null }""", "schedule is missing")]
    [InlineData("""{ "schedule": [ { "days": [], "start": "08:00", "end": "09:00" } ] }""", "a schedule range has no days")]
    [InlineData("""{ "schedule": [ { "days": ["Monday"], "start": "09:00", "end": "08:00" } ] }""", "a schedule range ends before it starts")]
    public void Invalid_values_are_rejected(string json, string expected)
    {
        Assert.False(SettingsJson.TryParse(json, out _, out var error));
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Invalid_time_is_rejected()
    {
        Assert.False(SettingsJson.TryParse("""{ "schedule": [ { "days": ["Monday"], "start": "8 o'clock", "end": "09:00" } ] }""", out _, out _));
    }
}
