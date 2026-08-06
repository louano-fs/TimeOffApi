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
        var zone = FindTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;
    }

    public static (DateTime Start, DateTime End) UtcDayBounds(DateTime utc, string timeZoneId)
    {
        var zone = FindTimeZone(timeZoneId);
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;
        var nextLocalDate = localDate.AddDays(1);
        var localDayStart = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var localDayEnd = DateTime.SpecifyKind(nextLocalDate, DateTimeKind.Unspecified);

        return (
            ResolveLocalBoundary(localDayStart, zone),
            ResolveLocalBoundary(localDayEnd, zone));
    }

    public static (DateTime Start, DateTime End) UtcDateRangeBounds(
        DateOnly startDate,
        DateOnly endDate,
        string timeZoneId)
    {
        if (startDate > endDate)
            throw new ValidationException("INVALID_DATE_RANGE", "Start date must not be after end date.");
        if (endDate == DateOnly.MaxValue)
            throw new ValidationException("INVALID_DATE_RANGE", "End date is outside the supported range.");

        var zone = FindTimeZone(timeZoneId);
        var localStart = DateTime.SpecifyKind(
            startDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            endDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        return (
            ResolveLocalBoundary(localStart, zone),
            ResolveLocalBoundary(localEnd, zone));
    }

    public static int Minutes(TimeSpan duration) =>
        Math.Max(0, (int)Math.Floor(duration.TotalMinutes));

    public static int Seconds(TimeSpan duration) =>
        Math.Max(0, (int)Math.Floor(duration.TotalSeconds));

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

    private static DateTime ResolveLocalBoundary(DateTime localBoundary, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(localBoundary))
            localBoundary = localBoundary.AddMinutes(1);

        if (!zone.IsAmbiguousTime(localBoundary))
            return TimeZoneInfo.ConvertTimeToUtc(localBoundary, zone);

        var earliestOffset = zone.GetAmbiguousTimeOffsets(localBoundary).Max();
        return new DateTimeOffset(localBoundary, earliestOffset).UtcDateTime;
    }
}
