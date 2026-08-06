using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITimeReportingService
{
    Task<PersonalTimeReportResponse> GetPersonalAsync(
        TimeReportQuery query,
        CancellationToken cancellationToken);
    Task<TeamTimeReportResponse> GetTeamAsync(
        TeamTimeReportQuery query,
        CancellationToken cancellationToken);
}

public sealed class TimeReportingService(
    AppDbContext db,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ITeamTimeReportingService teamReporting) : ITimeReportingService
{
    public async Task<PersonalTimeReportResponse> GetPersonalAsync(
        TimeReportQuery query,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        EnsureActive(user);

        var asOf = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(DateTimeHelper.LocalDate(asOf, user.Timezone));
        var (startDate, endDate) = ResolveDates(query, today, 366, "Personal reports");
        var works = await LoadWorkSessionsAsync(
            [user.Id], startDate, endDate, user.Timezone, asOf, cancellationToken);

        var daily = BuildDailyReport(works, startDate, endDate, user.Timezone, asOf);
        return new(
            startDate,
            endDate,
            user.Timezone,
            asOf,
            daily.Sum(x => x.WorkedSeconds),
            daily.Sum(x => x.BreakSeconds),
            works.Count,
            daily);
    }

    public async Task<TeamTimeReportResponse> GetTeamAsync(
        TeamTimeReportQuery query,
        CancellationToken cancellationToken)
    {
        var manager = await GetCurrentUserAsync(cancellationToken);
        EnsureActive(manager);
        if (manager.Role != UserRole.Manager)
            throw new ForbiddenException(
                "MANAGER_ACCESS_REQUIRED",
                "Current manager access is required.");

        var scope = new ManagerScope(
            manager.Id,
            manager.Timezone,
            timeProvider.GetUtcNow().UtcDateTime);
        return await teamReporting.GetAsync(scope, query, cancellationToken);
    }

    private async Task<User> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken)
        ?? throw new UnauthorizedException(
            "USER_NOT_FOUND",
            "The authenticated user no longer exists.");

    private static void EnsureActive(User user)
    {
        if (!user.IsActive)
            throw new ForbiddenException("USER_INACTIVE", "This account is inactive.");
    }

    private static (DateOnly StartDate, DateOnly EndDate) ResolveDates(
        TimeReportQuery query,
        DateOnly today,
        int maxDays,
        string reportName)
    {
        var startDate = query.StartDate ?? query.EndDate ?? today;
        var endDate = query.EndDate ?? query.StartDate ?? today;
        if (startDate > endDate)
            throw new ValidationException("INVALID_DATE_RANGE", "Start date must not be after end date.");
        if (startDate == DateOnly.MinValue)
            throw new ValidationException("INVALID_DATE_RANGE", "Start date is outside the supported range.");
        if (endDate == DateOnly.MaxValue)
            throw new ValidationException("INVALID_DATE_RANGE", "End date is outside the supported range.");

        var inclusiveDays = endDate.DayNumber - startDate.DayNumber + 1;
        if (inclusiveDays > maxDays)
            throw new ValidationException(
                "REPORT_RANGE_TOO_LARGE",
                $"{reportName} may span at most {maxDays} days.");

        return (startDate, endDate);
    }

    private async Task<List<TimeLog>> LoadWorkSessionsAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly startDate,
        DateOnly endDate,
        string reportingTimezone,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        var (rangeStart, configuredRangeEnd) = DateTimeHelper.UtcDateRangeBounds(
            startDate, endDate, reportingTimezone);
        var rangeEnd = configuredRangeEnd < asOf ? configuredRangeEnd : asOf;
        if (rangeEnd <= rangeStart)
            return [];

        return await db.TimeLogs.AsNoTracking()
            .Include(x => x.Breaks)
            .Where(x => userIds.Contains(x.UserId)
                && x.Type == TimeLogType.Work
                && !x.IsDeleted
                && x.Start < rangeEnd
                && (x.End == null || x.End > rangeStart))
            .OrderBy(x => x.Start)
            .ToListAsync(cancellationToken);
    }

    private static DailyTimeReportResponse[] BuildDailyReport(
        IReadOnlyCollection<TimeLog> works,
        DateOnly startDate,
        DateOnly endDate,
        string timezone,
        DateTime asOf)
    {
        var days = new List<DailyTimeReportResponse>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var (dayStart, configuredDayEnd) = DateTimeHelper.UtcDateRangeBounds(date, date, timezone);
            var dayEnd = configuredDayEnd < asOf ? configuredDayEnd : asOf;
            var durations = Aggregate(works, asOf, dayStart, dayEnd);
            days.Add(new(
                date,
                DateTimeHelper.Seconds(durations.Worked),
                DateTimeHelper.Seconds(durations.Break)));
        }

        return [.. days];
    }

    private static (TimeSpan Worked, TimeSpan Break) Aggregate(
        IEnumerable<TimeLog> works,
        DateTime asOf,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        if (rangeEnd <= rangeStart)
            return (TimeSpan.Zero, TimeSpan.Zero);

        return works.Aggregate(
            (Worked: TimeSpan.Zero, Break: TimeSpan.Zero),
            (total, work) =>
            {
                var duration = TimeLogMapper.ToDurationsWithin(work, asOf, rangeStart, rangeEnd);
                return (total.Worked + duration.Worked, total.Break + duration.Break);
            });
    }
}
