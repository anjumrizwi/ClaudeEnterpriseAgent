using System;

public class DateTimeTool
{
    public string Name => "get_datetime";

    public string Description =>
        "Returns the current date and time, optionally in a specific IANA timezone and format.";

    public object InputSchema => new
    {
        type = "object",
        properties = new
        {
            timezone = new
            {
                type = "string",
                description = "IANA timezone id, e.g. \"America/New_York\". Defaults to UTC.",
            },
            format = new
            {
                type = "string",
                description = "A .NET custom date/time format string, e.g. \"yyyy-MM-dd HH:mm:ss\". Defaults to ISO 8601.",
            },
        },
        required = Array.Empty<string>(),
    };

    public string Execute(string timezone = null, string format = null)
    {
        var utcNow = DateTimeOffset.UtcNow;

        var target = string.IsNullOrWhiteSpace(timezone)
            ? utcNow
            : TimeZoneInfo.ConvertTime(utcNow, ResolveTimeZone(timezone));

        return string.IsNullOrWhiteSpace(format)
            ? target.ToString("O")
            : target.ToString(format);
    }

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Unknown timezone: {timezone}", nameof(timezone));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException($"Invalid timezone data: {timezone}", nameof(timezone));
        }
    }
}
