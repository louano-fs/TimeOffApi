using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITeamTimeReportingService
{
    Task<TeamTimeReportResponse> GetAsync(
        ManagerScope scope,
        TeamTimeReportQuery query,
        CancellationToken cancellationToken);
}

public sealed class TeamTimeReportingService(AppDbContext db) : ITeamTimeReportingService
{
    private const int MaxTeamMembers = 200;
    private const int MaxRangeDays = 92;

    public async Task<TeamTimeReportResponse> GetAsync(
        ManagerScope scope,
        TeamTimeReportQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeHelper.LocalDate(scope.AsOf, scope.Timezone));
        var (startDate, endDate) = ResolveDates(query, today);

        var directReports = db.Users.AsNoTracking()
            .Where(x => x.ManagerId == scope.ManagerId && x.Role == UserRole.Employee);
        var excludedInactiveCount = query.IncludeInactive
            ? 0
            : await directReports.CountAsync(x => !x.IsActive, cancellationToken);
        if (!query.IncludeInactive)
            directReports = directReports.Where(x => x.IsActive);

        var includedMemberCount = await directReports.CountAsync(cancellationToken);
        if (includedMemberCount > MaxTeamMembers)
            throw new ValidationException(
                "TEAM_REPORT_TOO_LARGE",
                $"Team reports may include at most {MaxTeamMembers} employees.");

        var members = await directReports
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.EmployeeNumber)
            .Select(x => new TeamMemberIdentity(
                x.Id,
                x.EmployeeId,
                x.EmployeeNumber,
                x.FirstName,
                x.LastName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
        var works = await LoadWorkSessionsAsync(
            members.Select(x => x.UserId).ToArray(),
            startDate,
            endDate,
            scope.Timezone,
            scope.AsOf,
            cancellationToken);

        var (rangeStart, configuredRangeEnd) = DateTimeHelper.UtcDateRangeBounds(
            startDate, endDate, scope.Timezone);
        var rangeEnd = configuredRangeEnd < scope.AsOf ? configuredRangeEnd : scope.AsOf;
        var workByUser = works.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.ToArray());
        var memberReports = members.Select(member =>
        {
            var memberWorks = workByUser.GetValueOrDefault(member.UserId) ?? [];
            var durations = Aggregate(memberWorks, scope.AsOf, rangeStart, rangeEnd);
            return new TeamMemberTimeReportResponse(
                member.UserId,
                member.EmployeeId,
                member.EmployeeNumber,
                member.FirstName,
                member.LastName,
                member.IsActive,
                DateTimeHelper.Seconds(durations.Worked),
                DateTimeHelper.Seconds(durations.Break),
                memberWorks.Length);
        }).ToArray();

        var totalWorkedSeconds = memberReports.Sum(x => x.WorkedSeconds);
        return new(
            startDate,
            endDate,
            scope.Timezone,
            scope.AsOf,
            memberReports.Length,
            excludedInactiveCount,
            totalWorkedSeconds,
            memberReports.Sum(x => x.BreakSeconds),
            memberReports.Length == 0 ? null : totalWorkedSeconds / (double)memberReports.Length,
            memberReports);
    }

    private static (DateOnly StartDate, DateOnly EndDate) ResolveDates(
        TimeReportQuery query,
        DateOnly today)
    {
        var startDate = query.StartDate ?? query.EndDate ?? today;
        var endDate = query.EndDate ?? query.StartDate ?? today;
        if (startDate > endDate)
            throw new ValidationException(
                "INVALID_DATE_RANGE",
                "Start date must not be after end date.");
        if (startDate == DateOnly.MinValue || endDate == DateOnly.MaxValue)
            throw new ValidationException(
                "INVALID_DATE_RANGE",
                "The date range is outside the supported range.");

        var inclusiveDays = endDate.DayNumber - startDate.DayNumber + 1;
        if (inclusiveDays > MaxRangeDays)
            throw new ValidationException(
                "REPORT_RANGE_TOO_LARGE",
                $"Team reports may span at most {MaxRangeDays} days.");

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

    private sealed record TeamMemberIdentity(
        int UserId,
        int EmployeeId,
        string EmployeeNumber,
        string FirstName,
        string LastName,
        bool IsActive);
}
