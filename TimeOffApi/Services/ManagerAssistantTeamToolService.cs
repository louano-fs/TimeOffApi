using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface IManagerAssistantTeamToolService
{
    Task<TeamWorkedTimeToolResult> GetTeamWorkedTimeAsync(
        ManagerScope scope,
        TeamWorkedTimeArguments arguments,
        CancellationToken cancellationToken);
    Task<TeamWorkedTimeThresholdToolResult> FindTeamMembersByWorkedTimeAsync(
        ManagerScope scope,
        TeamWorkedTimeThresholdArguments arguments,
        CancellationToken cancellationToken);
    Task<ManagerAssistantToolResult> GetDirectReportWorkedTimeAsync(
        ManagerScope scope,
        DirectReportWorkedTimeArguments arguments,
        CancellationToken cancellationToken);
    Task<TeamCurrentStatusToolResult> GetTeamCurrentStatusAsync(
        ManagerScope scope,
        TeamCurrentStatusArguments arguments,
        CancellationToken cancellationToken);
    Task<TeamTimeLogExportToolResult> PrepareTeamTimeLogExportAsync(
        ManagerScope scope,
        TeamTimeLogExportArguments arguments,
        CancellationToken cancellationToken);
}

public sealed class ManagerAssistantTeamToolService(
    AppDbContext db,
    ITeamTimeReportingService reporting,
    IDirectReportResolver directReportResolver,
    IOptions<ManagerAssistantOptions> options) : IManagerAssistantTeamToolService
{
    private const int MaxExportTeamMembers = 500;
    private const int MaxExportRangeDays = 366;
    private readonly ManagerAssistantOptions _options = options.Value;

    public async Task<TeamWorkedTimeToolResult> GetTeamWorkedTimeAsync(
        ManagerScope scope,
        TeamWorkedTimeArguments arguments,
        CancellationToken cancellationToken)
    {
        EnsureInteractiveRange(arguments.StartDate, arguments.EndDate);
        var report = await GetReportAsync(scope, arguments, cancellationToken);
        EnsureInteractiveMemberCount(report.IncludedMemberCount);
        var statusByUser = await LoadStatusesAsync(
            report.Members.Select(x => x.UserId).ToArray(), scope.AsOf, cancellationToken);
        var ordered = Order(report.Members, arguments.Order).ToArray();
        EnsureLimit(arguments.Limit, ordered.Length);
        var ranks = Rank(ordered, arguments.Order);
        if (arguments.Limit.HasValue)
            ordered = ordered.Take(arguments.Limit.Value).ToArray();

        return new(
            report.StartDate,
            report.EndDate,
            report.ReportingTimezone,
            report.AsOf,
            IsPeriodComplete(report.EndDate, scope),
            report.IncludedMemberCount,
            report.ExcludedInactiveCount,
            report.TotalWorkedSeconds,
            report.TotalBreakSeconds,
            report.AverageWorkedSeconds,
            arguments.Order,
            ordered.Select(x => ToEvidence(
                x,
                statusByUser.GetValueOrDefault(x.UserId, TeamClockStatus.ClockedOut),
                ranks.GetValueOrDefault(x.UserId)))
                .ToArray());
    }

    public async Task<TeamWorkedTimeThresholdToolResult> FindTeamMembersByWorkedTimeAsync(
        ManagerScope scope,
        TeamWorkedTimeThresholdArguments arguments,
        CancellationToken cancellationToken)
    {
        var thresholdSeconds = ToThresholdSeconds(arguments.ThresholdValue, arguments.ThresholdUnit);
        EnsureInteractiveRange(arguments.StartDate, arguments.EndDate);
        var report = await reporting.GetAsync(
            scope,
            new TeamTimeReportQuery
            {
                StartDate = arguments.StartDate,
                EndDate = arguments.EndDate,
                IncludeInactive = arguments.IncludeInactive
            },
            cancellationToken);
        EnsureInteractiveMemberCount(report.IncludedMemberCount);
        var matches = report.Members
            .Where(x => IsMatch(x.WorkedSeconds, thresholdSeconds, arguments.Comparison))
            .OrderBy(x => x.WorkedSeconds)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.EmployeeNumber)
            .ToArray();
        var statusByUser = await LoadStatusesAsync(
            matches.Select(x => x.UserId).ToArray(), scope.AsOf, cancellationToken);

        return new(
            report.StartDate,
            report.EndDate,
            report.ReportingTimezone,
            report.AsOf,
            IsPeriodComplete(report.EndDate, scope),
            arguments.Comparison,
            thresholdSeconds,
            report.IncludedMemberCount,
            report.ExcludedInactiveCount,
            matches.Length,
            matches.Select(x => ToEvidence(
                x,
                statusByUser.GetValueOrDefault(x.UserId, TeamClockStatus.ClockedOut),
                rank: null))
                .ToArray());
    }

    public async Task<ManagerAssistantToolResult> GetDirectReportWorkedTimeAsync(
        ManagerScope scope,
        DirectReportWorkedTimeArguments arguments,
        CancellationToken cancellationToken)
    {
        EnsureInteractiveRange(arguments.StartDate, arguments.EndDate);
        var resolution = await directReportResolver.ResolveAsync(
            scope,
            arguments.EmployeeReference,
            arguments.IncludeInactive,
            cancellationToken);
        if (resolution is AmbiguousDirectReport ambiguous)
            return new TeamMemberClarificationToolResult(ambiguous.Candidates);

        var resolved = (ResolvedDirectReport)resolution;
        var report = await reporting.GetAsync(
            scope,
            new TeamTimeReportQuery
            {
                StartDate = arguments.StartDate,
                EndDate = arguments.EndDate,
                IncludeInactive = arguments.IncludeInactive
            },
            cancellationToken);
        EnsureInteractiveMemberCount(report.IncludedMemberCount);
        var member = report.Members.SingleOrDefault(x => x.UserId == resolved.Member.UserId)
            ?? throw new NotFoundException(
                "TEAM_MEMBER_NOT_FOUND",
                "The requested team member was not found.");
        var statusByUser = await LoadStatusesAsync([member.UserId], scope.AsOf, cancellationToken);

        return new DirectReportWorkedTimeToolResult(
            report.StartDate,
            report.EndDate,
            report.ReportingTimezone,
            report.AsOf,
            IsPeriodComplete(report.EndDate, scope),
            ToEvidence(
                member,
                statusByUser.GetValueOrDefault(member.UserId, TeamClockStatus.ClockedOut),
                rank: null));
    }

    public async Task<TeamCurrentStatusToolResult> GetTeamCurrentStatusAsync(
        ManagerScope scope,
        TeamCurrentStatusArguments arguments,
        CancellationToken cancellationToken)
    {
        var (members, excludedInactiveCount) = await LoadMembersAsync(
            scope,
            arguments.IncludeInactive,
            _options.MaxTeamMembers,
            cancellationToken);
        var statusByUser = await LoadStatusesAsync(
            members.Select(x => x.UserId).ToArray(), scope.AsOf, cancellationToken);

        return new(
            scope.Timezone,
            scope.AsOf,
            members.Length,
            excludedInactiveCount,
            members.Select(x => new TeamStatusEvidence(
                x.EmployeeNumber,
                x.DisplayName,
                x.IsActive,
                statusByUser.GetValueOrDefault(x.UserId, TeamClockStatus.ClockedOut)))
                .ToArray());
    }

    public async Task<TeamTimeLogExportToolResult> PrepareTeamTimeLogExportAsync(
        ManagerScope scope,
        TeamTimeLogExportArguments arguments,
        CancellationToken cancellationToken)
    {
        EnsureExportRange(arguments.StartDate, arguments.EndDate);
        var (members, excludedInactiveCount) = await LoadMembersAsync(
            scope,
            arguments.IncludeInactive,
            MaxExportTeamMembers,
            cancellationToken);
        var start = arguments.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = arguments.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var includeInactive = arguments.IncludeInactive ? "true" : "false";

        return new(
            arguments.StartDate,
            arguments.EndDate,
            scope.Timezone,
            scope.AsOf,
            arguments.IncludeInactive,
            members.Length,
            excludedInactiveCount,
            $"team-time-logs-{start}-to-{end}.xlsx",
            $"/api/team/time-logs/export?startDate={start}&endDate={end}"
                + $"&includeInactive={includeInactive}&format=xlsx");
    }

    private Task<TeamTimeReportResponse> GetReportAsync(
        ManagerScope scope,
        TeamWorkedTimeArguments arguments,
        CancellationToken cancellationToken) =>
        reporting.GetAsync(
            scope,
            new TeamTimeReportQuery
            {
                StartDate = arguments.StartDate,
                EndDate = arguments.EndDate,
                IncludeInactive = arguments.IncludeInactive
            },
            cancellationToken);

    private async Task<(AssistantMember[] Members, int ExcludedInactiveCount)> LoadMembersAsync(
        ManagerScope scope,
        bool includeInactive,
        int maxMembers,
        CancellationToken cancellationToken)
    {
        var directReports = db.Users.AsNoTracking()
            .Where(x => x.ManagerId == scope.ManagerId && x.Role == UserRole.Employee);
        var excludedInactiveCount = includeInactive
            ? 0
            : await directReports.CountAsync(x => !x.IsActive, cancellationToken);
        if (!includeInactive)
            directReports = directReports.Where(x => x.IsActive);

        var members = await directReports
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.EmployeeNumber)
            .Take(maxMembers + 1)
            .Select(x => new AssistantMember(
                x.Id,
                x.EmployeeNumber,
                x.FirstName,
                x.LastName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
        if (members.Length > maxMembers)
            throw new ValidationException(
                "TEAM_REPORT_TOO_LARGE",
                $"This request may include at most {maxMembers} employees.");

        return (members, excludedInactiveCount);
    }

    private async Task<Dictionary<int, TeamClockStatus>> LoadStatusesAsync(
        IReadOnlyCollection<int> userIds,
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        var workingUserIds = await db.TimeLogs.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId)
                && x.Type == TimeLogType.Work
                && !x.IsDeleted
                && x.Start <= asOf
                && (x.End == null || x.End > asOf))
            .Select(x => x.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var breakUserIds = await db.TimeLogs.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId)
                && x.Type == TimeLogType.Break
                && !x.IsDeleted
                && x.Start <= asOf
                && (x.End == null || x.End > asOf)
                && x.ParentTimeLog != null
                && !x.ParentTimeLog.IsDeleted
                && x.ParentTimeLog.Start <= asOf
                && (x.ParentTimeLog.End == null || x.ParentTimeLog.End > asOf))
            .Select(x => x.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var statuses = workingUserIds.ToDictionary(x => x, _ => TeamClockStatus.Working);
        foreach (var userId in breakUserIds)
            statuses[userId] = TeamClockStatus.OnBreak;
        return statuses;
    }

    private static IEnumerable<TeamMemberTimeReportResponse> Order(
        IEnumerable<TeamMemberTimeReportResponse> members,
        TeamWorkedTimeOrder order) =>
        order switch
        {
            TeamWorkedTimeOrder.WorkedAscending => members
                .OrderBy(x => x.WorkedSeconds)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.EmployeeNumber),
            TeamWorkedTimeOrder.WorkedDescending => members
                .OrderByDescending(x => x.WorkedSeconds)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.EmployeeNumber),
            TeamWorkedTimeOrder.Name => members
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.EmployeeNumber),
            _ => throw new ValidationException("INVALID_ORDER", "The requested order is invalid.")
        };

    private static Dictionary<int, int?> Rank(
        IReadOnlyList<TeamMemberTimeReportResponse> members,
        TeamWorkedTimeOrder order)
    {
        var ranks = new Dictionary<int, int?>();
        if (order == TeamWorkedTimeOrder.Name)
        {
            foreach (var member in members)
                ranks[member.UserId] = null;
            return ranks;
        }

        int? previousSeconds = null;
        var rank = 0;
        for (var index = 0; index < members.Count; index++)
        {
            if (previousSeconds != members[index].WorkedSeconds)
                rank = index + 1;
            ranks[members[index].UserId] = rank;
            previousSeconds = members[index].WorkedSeconds;
        }
        return ranks;
    }

    private static TeamWorkedTimeEvidence ToEvidence(
        TeamMemberTimeReportResponse member,
        TeamClockStatus status,
        int? rank) =>
        new(
            member.EmployeeNumber,
            $"{member.FirstName} {member.LastName}".Trim(),
            member.IsActive,
            member.WorkedSeconds,
            member.BreakSeconds,
            status,
            rank);

    private static int ToThresholdSeconds(decimal value, WorkedTimeUnit unit)
    {
        if (value < 0)
            throw new ValidationException("INVALID_THRESHOLD", "The threshold cannot be negative.");

        var seconds = value * unit switch
        {
            WorkedTimeUnit.Seconds => 1m,
            WorkedTimeUnit.Minutes => 60m,
            WorkedTimeUnit.Hours => 3_600m,
            _ => throw new ValidationException("INVALID_THRESHOLD", "The threshold unit is invalid.")
        };
        if (seconds != decimal.Truncate(seconds) || seconds > int.MaxValue)
            throw new ValidationException(
                "INVALID_THRESHOLD",
                "The threshold must resolve to a whole number of supported seconds.");
        return decimal.ToInt32(seconds);
    }

    private static bool IsMatch(
        int workedSeconds,
        int thresholdSeconds,
        WorkedTimeComparison comparison) =>
        comparison switch
        {
            WorkedTimeComparison.LessThan => workedSeconds < thresholdSeconds,
            WorkedTimeComparison.LessThanOrEqual => workedSeconds <= thresholdSeconds,
            WorkedTimeComparison.GreaterThan => workedSeconds > thresholdSeconds,
            WorkedTimeComparison.GreaterThanOrEqual => workedSeconds >= thresholdSeconds,
            _ => throw new ValidationException(
                "INVALID_COMPARISON",
                "The threshold comparison is invalid.")
        };

    private static bool IsPeriodComplete(DateOnly endDate, ManagerScope scope)
    {
        var (_, rangeEnd) = DateTimeHelper.UtcDateRangeBounds(endDate, endDate, scope.Timezone);
        return rangeEnd <= scope.AsOf;
    }

    private static void EnsureLimit(int? limit, int includedMemberCount)
    {
        if (limit is null)
            return;
        if (limit < 1 || limit > includedMemberCount)
            throw new ValidationException(
                "INVALID_LIMIT",
                "The result limit must be between one and the included team size.");
    }

    private static void EnsureExportRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate
            || startDate == DateOnly.MinValue
            || endDate == DateOnly.MaxValue)
            throw new ValidationException(
                "INVALID_DATE_RANGE",
                "The export date range is invalid.");
        var inclusiveDays = endDate.DayNumber - startDate.DayNumber + 1;
        if (inclusiveDays > MaxExportRangeDays)
            throw new ValidationException(
                "REPORT_RANGE_TOO_LARGE",
                $"Team exports may span at most {MaxExportRangeDays} days.");
    }

    private void EnsureInteractiveRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate
            || startDate == DateOnly.MinValue
            || endDate == DateOnly.MaxValue)
            throw new ValidationException(
                "INVALID_DATE_RANGE",
                "The report date range is invalid.");
        var inclusiveDays = endDate.DayNumber - startDate.DayNumber + 1;
        if (inclusiveDays > _options.MaxTeamRangeDays)
            throw new ValidationException(
                "REPORT_RANGE_TOO_LARGE",
                $"Team reports may span at most {_options.MaxTeamRangeDays} days.");
    }

    private void EnsureInteractiveMemberCount(int includedMemberCount)
    {
        if (includedMemberCount > _options.MaxTeamMembers)
            throw new ValidationException(
                "TEAM_REPORT_TOO_LARGE",
                $"Team reports may include at most {_options.MaxTeamMembers} employees.");
    }

    private sealed record AssistantMember(
        int UserId,
        string EmployeeNumber,
        string FirstName,
        string LastName,
        bool IsActive)
    {
        public string DisplayName => $"{FirstName} {LastName}".Trim();
    }
}
