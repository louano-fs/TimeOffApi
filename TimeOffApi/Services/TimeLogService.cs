using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITimeLogService
{
    Task<PagedResponse<WorkSessionResponse>> GetMineAsync(
        TimeLogQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<WorkSessionResponse>> GetAdminAsync(
        AdminTimeLogQuery query, CancellationToken cancellationToken);
    Task<PagedResponse<WorkSessionResponse>> GetTeamMemberAsync(
        int userId, TimeLogQuery query, CancellationToken cancellationToken);
    Task<WorkSessionResponse> GetMineByIdAsync(int id, CancellationToken cancellationToken);
    Task<TimeSummaryResponse> GetSummaryAsync(
        SummaryQuery query, CancellationToken cancellationToken);
}

public sealed class TimeLogService(
    AppDbContext db,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ITimeLogService
{
    public Task<PagedResponse<WorkSessionResponse>> GetMineAsync(
        TimeLogQuery query,
        CancellationToken cancellationToken) =>
        GetPagedAsync(
            db.TimeLogs.Where(x => x.UserId == currentUser.UserId),
            query,
            cancellationToken);

    public Task<PagedResponse<WorkSessionResponse>> GetAdminAsync(
        AdminTimeLogQuery query,
        CancellationToken cancellationToken)
    {
        var source = db.TimeLogs.AsQueryable();
        if (query.EmployeeId.HasValue)
            source = source.Where(x => x.User.EmployeeId == query.EmployeeId);
        return GetPagedAsync(source, query, cancellationToken);
    }

    public async Task<PagedResponse<WorkSessionResponse>> GetTeamMemberAsync(
        int userId,
        TimeLogQuery query,
        CancellationToken cancellationToken)
    {
        var isDirectReport = await db.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId
                && x.ManagerId == currentUser.UserId
                && x.Role == UserRole.Employee, cancellationToken);
        if (!isDirectReport)
            throw new NotFoundException("TEAM_MEMBER_NOT_FOUND", "Team member was not found.");

        return await GetPagedAsync(
            db.TimeLogs.Where(x => x.UserId == userId),
            query,
            cancellationToken);
    }

    public async Task<WorkSessionResponse> GetMineByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var work = await db.TimeLogs.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Breaks)
            .SingleOrDefaultAsync(x => x.Id == id
                && x.UserId == currentUser.UserId
                && x.Type == TimeLogType.Work
                && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("TIME_LOG_NOT_FOUND", "Time log was not found.");

        return TimeLogMapper.ToWorkSession(work, timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<TimeSummaryResponse> GetSummaryAsync(
        SummaryQuery query,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken)
            ?? throw new UnauthorizedException("USER_NOT_FOUND", "The authenticated user no longer exists.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(DateTimeHelper.LocalDate(now, user.Timezone));
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var currentWeekStart = today.AddDays(-daysSinceMonday);
        var start = query.StartDate
            ?? (query.EndDate.HasValue ? query.EndDate.Value : currentWeekStart);
        var end = query.EndDate
            ?? (query.StartDate.HasValue ? query.StartDate.Value : currentWeekStart.AddDays(6));

        var startValue = start.ToDateTime(TimeOnly.MinValue);
        var endValue = end.ToDateTime(TimeOnly.MinValue);
        var works = await db.TimeLogs.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Breaks)
            .Where(x => x.UserId == user.Id
                && x.Type == TimeLogType.Work
                && !x.IsDeleted
                && x.ShiftDate >= startValue
                && x.ShiftDate <= endValue
                && (x.End != null || x.ShiftDate == today.ToDateTime(TimeOnly.MinValue)))
            .OrderBy(x => x.ShiftDate)
            .ThenBy(x => x.Start)
            .ToListAsync(cancellationToken);

        var sessions = works.Select(x => TimeLogMapper.ToWorkSession(x, now)).ToList();
        var daily = sessions
            .GroupBy(x => x.ShiftDate)
            .Select(x => new DailySummaryResponse(
                x.Key,
                x.Sum(y => y.TotalWorkedMinutes),
                x.Sum(y => y.TotalBreakMinutes)))
            .OrderBy(x => x.Date)
            .ToArray();

        return new(
            start,
            end,
            sessions.Sum(x => x.TotalWorkedMinutes),
            sessions.Sum(x => x.TotalBreakMinutes),
            sessions.Count(x => x.Status == "Completed"),
            daily);
    }

    private async Task<PagedResponse<WorkSessionResponse>> GetPagedAsync(
        IQueryable<TimeLog> source,
        TimeLogQuery query,
        CancellationToken cancellationToken)
    {
        source = source.Where(x => x.Type == TimeLogType.Work && !x.IsDeleted);
        if (query.StartDate.HasValue)
        {
            var start = query.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            source = source.Where(x => x.ShiftDate >= start);
        }
        if (query.EndDate.HasValue)
        {
            var end = query.EndDate.Value.ToDateTime(TimeOnly.MinValue);
            source = source.Where(x => x.ShiftDate <= end);
        }
        if (query.Status?.Equals("active", StringComparison.OrdinalIgnoreCase) == true)
            source = source.Where(x => x.End == null);
        else if (query.Status?.Equals("completed", StringComparison.OrdinalIgnoreCase) == true)
            source = source.Where(x => x.End != null);

        var totalCount = await source.CountAsync(cancellationToken);
        var works = await source.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Breaks)
            .OrderByDescending(x => x.ShiftDate)
            .ThenByDescending(x => x.Start)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var items = works.Select(x => TimeLogMapper.ToWorkSession(x, now)).ToArray();

        return new(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }
}
