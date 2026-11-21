using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BreakWindow.Core;

public static class SettingsJson
{
    public static string Serialize(Settings settings) =>
        JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings).ReplaceLineEndings("\n") + "\n";

    public static bool TryParse(string json, out Settings settings, out string error)
    {
        settings = new Settings();
        Settings? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.Settings);
        }
        catch (JsonException e)
        {
            error = e.LineNumber is { } line ? $"error on line {line + 1}" : "not valid JSON";
            return false;
        }

        if (parsed is null)
        {
            error = "not valid JSON";
            return false;
        }

        if (Validate(parsed) is { } problem)
        {
            error = problem;
            return false;
        }

        settings = parsed;
        error = "";
        return true;
    }

    private static string? Validate(Settings s)
    {
        if (s.Schedule is null)
        {
            return "schedule is missing";
        }

        foreach (var range in s.Schedule)
        {
            if (range is null || range.Days is null || range.Days.Count == 0)
            {
                return "a schedule range has no days";
            }

            if (range.End <= range.Start)
            {
                return "a schedule range ends before it starts";
            }
        }

        if (s.WorkMinutes is < 1 or > 1440)
        {
            return "workMinutes must be 1 to 1440";
        }

        if (s.BreakMinutes is < 1 or > 1440)
        {
            return "breakMinutes must be 1 to 1440";
        }

        if (s.IdleGraceSeconds is < 1 or > 86400)
        {
            return "idleGraceSeconds must be 1 to 86400";
        }

        return null;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(TimeOfDayConverter)])]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

internal sealed class TimeOfDayConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (TimeOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        throw new JsonException($"'{text}' is not a time of day");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
}
