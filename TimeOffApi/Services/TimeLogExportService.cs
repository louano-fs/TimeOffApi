using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITimeLogExportService
{
    Task<TimeLogExportFile> ExportPersonalAsync(
        TimeLogExportQuery query,
        CancellationToken cancellationToken);
    Task<TimeLogExportFile> ExportTeamAsync(
        TeamTimeLogExportQuery query,
        CancellationToken cancellationToken);
}

internal sealed class TimeLogExportService(
    AppDbContext db,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ITimeLogWorkbookWriter workbookWriter) : ITimeLogExportService
{
    private const int MaxPersonalSessions = 10_000;
    private const int MaxTeamMembers = 500;
    private const int MaxTeamSessions = 50_000;

    public async Task<TimeLogExportFile> ExportPersonalAsync(
        TimeLogExportQuery query,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        EnsureActive(user);
        EnsureXlsx(query.Format);

        var asOf = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(DateTimeHelper.LocalDate(asOf, user.Timezone));
        var (startDate, endDate) = ResolveDates(query, today);
        var works = await LoadWorkSessionsAsync(
            [user.Id],
            startDate,
            endDate,
            user.Timezone,
            asOf,
            MaxPersonalSessions,
            cancellationToken);
        var member = ToMember(user);
        var export = BuildExportData(
            "Personal",
            FullName(user),
            user.EmployeeNumber,
            user.Timezone,
            startDate,
            endDate,
            asOf,
            0,
            [member],
            works);

        return workbookWriter.Write(export);
    }

    public async Task<TimeLogExportFile> ExportTeamAsync(
        TeamTimeLogExportQuery query,
        CancellationToken cancellationToken)
    {
        var manager = await GetCurrentUserAsync(cancellationToken);
        EnsureActive(manager);
        if (manager.Role != UserRole.Manager)
            throw new ForbiddenException(
                "MANAGER_ACCESS_REQUIRED",
                "Current manager access is required.");
        EnsureXlsx(query.Format);

        var asOf = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(DateTimeHelper.LocalDate(asOf, manager.Timezone));
        var (startDate, endDate) = ResolveDates(query, today, 366, "Team exports");
        var directReports = db.Users.AsNoTracking()
            .Where(x => x.ManagerId == manager.Id && x.Role == UserRole.Employee);
        var excludedInactiveCount = query.IncludeInactive
            ? 0
            : await directReports.CountAsync(x => !x.IsActive, cancellationToken);
        if (!query.IncludeInactive)
            directReports = directReports.Where(x => x.IsActive);

        var members = await directReports
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.EmployeeNumber)
            .Take(MaxTeamMembers + 1)
            .Select(x => new ExportMember(
                x.Id,
                x.EmployeeId,
                x.EmployeeNumber,
                x.FirstName,
                x.LastName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
        if (members.Length > MaxTeamMembers)
            throw ExportTooLarge(
                $"Team exports may include at most {MaxTeamMembers} employees.");

        var works = await LoadWorkSessionsAsync(
            members.Select(x => x.UserId).ToArray(),
            startDate,
            endDate,
            manager.Timezone,
            asOf,
            MaxTeamSessions,
            cancellationToken);
        var export = BuildExportData(
            "Team",
            FullName(manager),
            null,
            manager.Timezone,
            startDate,
            endDate,
            asOf,
            excludedInactiveCount,
            members,
            works);

        return workbookWriter.Write(export);
    }

    private async Task<User> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken)
        ?? throw new UnauthorizedException(
            "USER_NOT_FOUND",
            "The authenticated user no longer exists.");

    private async Task<TimeLog[]> LoadWorkSessionsAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly startDate,
        DateOnly endDate,
        string reportingTimezone,
        DateTime asOf,
        int maxSessions,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        var (rangeStart, configuredRangeEnd) = DateTimeHelper.UtcDateRangeBounds(
            startDate, endDate, reportingTimezone);
        var rangeEnd = configuredRangeEnd < asOf ? configuredRangeEnd : asOf;
        if (rangeEnd <= rangeStart)
            return [];

        var works = await db.TimeLogs.AsNoTracking()
            .Include(x => x.Breaks)
            .Where(x => userIds.Contains(x.UserId)
                && x.Type == TimeLogType.Work
                && !x.IsDeleted
                && x.Start < rangeEnd
                && (x.End == null || x.End > rangeStart))
            .OrderBy(x => x.Start)
            .ThenBy(x => x.Id)
            .Take(maxSessions + 1)
            .ToArrayAsync(cancellationToken);
        if (works.Length > maxSessions)
            throw ExportTooLarge(
                $"Exports may include at most {maxSessions:N0} work sessions.");

        return works;
    }

    private static TimeLogExportData BuildExportData(
        string reportType,
        string preparedFor,
        string? preparedForEmployeeNumber,
        string reportingTimezone,
        DateOnly startDate,
        DateOnly endDate,
        DateTime asOf,
        int excludedInactiveCount,
        IReadOnlyList<ExportMember> members,
        IReadOnlyCollection<TimeLog> works)
    {
        var (rangeStart, configuredRangeEnd) = DateTimeHelper.UtcDateRangeBounds(
            startDate, endDate, reportingTimezone);
        var rangeEnd = configuredRangeEnd < asOf ? configuredRangeEnd : asOf;
        var membersById = members.ToDictionary(x => x.UserId);
        var sessions = new List<ExportWorkSession>(works.Count);
        var breaks = new List<ExportBreak>();

        foreach (var work in works)
        {
            var member = membersById[work.UserId];
            var workStart = Later(TimeLogMapper.Utc(work.Start), rangeStart);
            var effectiveWorkEnd = work.End is null ? asOf : TimeLogMapper.Utc(work.End.Value);
            var workEnd = Earlier(effectiveWorkEnd, rangeEnd);
            if (workEnd <= workStart)
                continue;

            var employeeName = $"{member.FirstName} {member.LastName}".Trim();
            var sessionBreaks = work.Breaks
                .Where(x => !x.IsDeleted)
                .Select(item => ClipBreak(
                    item,
                    work,
                    member,
                    employeeName,
                    reportingTimezone,
                    asOf,
                    workStart,
                    workEnd))
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.Start)
                .ThenBy(x => x.SourceBreakId)
                .ToArray();
            breaks.AddRange(sessionBreaks);

            var durations = TimeLogMapper.ToDurationsWithin(
                work, asOf, rangeStart, rangeEnd);
            var elapsed = durations.Worked + durations.Break;
            sessions.Add(new(
                work.Id,
                work.UserId,
                member.EmployeeNumber,
                employeeName,
                DateOnly.FromDateTime(work.ShiftDate),
                DateTimeHelper.LocalDateTime(workStart, reportingTimezone),
                DateTimeHelper.LocalDateTime(workEnd, reportingTimezone),
                work.End is null ? "Active" : "Completed",
                work.Timezone,
                DateTimeHelper.Seconds(elapsed),
                DateTimeHelper.Seconds(durations.Break),
                DateTimeHelper.Seconds(durations.Worked)));
        }

        return new(
            reportType,
            preparedFor,
            preparedForEmployeeNumber,
            reportingTimezone,
            startDate,
            endDate,
            asOf,
            excludedInactiveCount,
            members,
            sessions,
            breaks);
    }

    private static ExportBreak? ClipBreak(
        TimeLog breakLog,
        TimeLog work,
        ExportMember member,
        string employeeName,
        string reportingTimezone,
        DateTime asOf,
        DateTime workStart,
        DateTime workEnd)
    {
        var breakStart = Later(TimeLogMapper.Utc(breakLog.Start), workStart);
        var effectiveBreakEnd = breakLog.End is null ? asOf : TimeLogMapper.Utc(breakLog.End.Value);
        var breakEnd = Earlier(effectiveBreakEnd, workEnd);
        if (breakEnd <= breakStart)
            return null;

        return new(
            breakLog.Id,
            work.Id,
            work.UserId,
            member.EmployeeNumber,
            employeeName,
            DateTimeHelper.LocalDateTime(breakStart, reportingTimezone),
            DateTimeHelper.LocalDateTime(breakEnd, reportingTimezone),
            breakLog.End is null ? "Active" : "Completed",
            DateTimeHelper.Seconds(breakEnd - breakStart));
    }

    private static ExportMember ToMember(User user) =>
        new(
            user.Id,
            user.EmployeeId,
            user.EmployeeNumber,
            user.FirstName,
            user.LastName,
            user.IsActive);

    private static string FullName(User user) => $"{user.FirstName} {user.LastName}".Trim();

    private static void EnsureActive(User user)
    {
        if (!user.IsActive)
            throw new ForbiddenException("USER_INACTIVE", "This account is inactive.");
    }

    private static void EnsureXlsx(string format)
    {
        if (!string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("INVALID_EXPORT_FORMAT", "Format must be xlsx.");
    }

    private static (DateOnly StartDate, DateOnly EndDate) ResolveDates(
        TimeLogExportQuery query,
        DateOnly today,
        int? maxDays = null,
        string reportName = "Exports")
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
        if (maxDays.HasValue && inclusiveDays > maxDays.Value)
            throw new ValidationException(
                "EXPORT_TOO_LARGE",
                $"{reportName} may span at most {maxDays.Value} days.");

        return (startDate, endDate);
    }

    private static ValidationException ExportTooLarge(string message) =>
        new("EXPORT_TOO_LARGE", message);

    private static DateTime Earlier(DateTime first, DateTime second) =>
        first < second ? first : second;

    private static DateTime Later(DateTime first, DateTime second) =>
        first > second ? first : second;
}
