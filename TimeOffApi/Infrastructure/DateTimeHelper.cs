using System.Globalization;
using System.Text.RegularExpressions;

namespace TimeOffApi.Infrastructure;

public static partial class DateTimeHelper
{
    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();

    public static DateTime ParseUtc(string? value, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException("DATE_TIME_REQUIRED", "Date and time is required.");
        if (!OffsetSuffix().IsMatch(value))
            throw new ValidationException("OFFSET_REQUIRED", "Timestamp must include Z or a UTC offset.");
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            throw new ValidationException("INVALID_DATE_TIME", "Timestamp must be valid ISO 8601.");
        if (parsed > utcNow.AddMinutes(5))
            throw new ValidationException("DATE_TIME_IN_FUTURE", "Timestamp cannot be more than five minutes in the future.");

        return parsed.UtcDateTime;
    }

    public static DateTime LocalDate(DateTime utc, string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException("INVALID_TIMEZONE", "The user's configured timezone is invalid.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ValidationException("INVALID_TIMEZONE", "The user's configured timezone is invalid.");
        }
    }

    public static int Minutes(TimeSpan duration) =>
        Math.Max(0, (int)Math.Floor(duration.TotalMinutes));
}
