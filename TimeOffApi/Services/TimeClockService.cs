using System.Data;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITimeClockService
{
    Task<TimeLogResponse> ClockInAsync(string? dateTime, CancellationToken cancellationToken);
    Task<TimeLogResponse> StartBreakAsync(string? dateTime, CancellationToken cancellationToken);
    Task<TimeLogResponse> EndBreakAsync(string? dateTime, CancellationToken cancellationToken);
    Task<TimeLogResponse> ClockOutAsync(string? dateTime, CancellationToken cancellationToken);
    Task<ClockStatusResponse> GetStatusAsync(int? requestedUserId, CancellationToken cancellationToken);
}

public sealed class TimeClockService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IUserLockService userLocks,
    TimeProvider timeProvider) : ITimeClockService
{
    public Task<TimeLogResponse> ClockInAsync(string? dateTime, CancellationToken cancellationToken) =>
        RunClockActionAsync(async (user, utcNow, ct) =>
        {
            var start = DateTimeHelper.ParseUtc(dateTime, utcNow);
            if (await db.TimeLogs.AnyAsync(x => x.UserId == user.Id
                    && x.Type == TimeLogType.Work && !x.IsDeleted
                    && (x.End == null || x.End > start), ct))
                throw new ConflictException("ACTIVE_OR_OVERLAPPING_WORK_SESSION",
                    "You are already clocked in or the requested time overlaps an existing session.");

            var log = new TimeLog
            {
                UserId = user.Id,
                ShiftDate = DateTimeHelper.LocalDate(start, user.Timezone),
                Start = start,
                Type = TimeLogType.Work,
                Timezone = user.Timezone,
                CreatedAt = utcNow.UtcDateTime
            };
            db.TimeLogs.Add(log);
            await SaveClockActionAsync("ACTIVE_WORK_SESSION_EXISTS",
                "You are already clocked in.", ct);
            return TimeLogMapper.ToClockResponse(log, utcNow.UtcDateTime);
        }, cancellationToken);

    public Task<TimeLogResponse> StartBreakAsync(string? dateTime, CancellationToken cancellationToken) =>
        RunClockActionAsync(async (user, utcNow, ct) =>
        {
            var start = DateTimeHelper.ParseUtc(dateTime, utcNow);
            var work = await ActiveWorkAsync(user.Id, ct)
                ?? throw new ConflictException("NO_ACTIVE_WORK_SESSION", "You are not clocked in.");
            if (start < TimeLogMapper.Utc(work.Start))
                throw new ValidationException("BREAK_BEFORE_CLOCK_IN",
                    "Break cannot begin before the work session.");
            if (work.Breaks.Any(x => !x.IsDeleted && x.End is null))
                throw new ConflictException("ACTIVE_BREAK_EXISTS", "A break is already active.");
            if (work.Breaks.Any(x => !x.IsDeleted && x.End > start))
                throw new ConflictException("BREAK_OVERLAP", "The break overlaps an existing break.");

            var breakLog = new TimeLog
            {
                UserId = user.Id,
                ParentTimeLogId = work.Id,
                ShiftDate = work.ShiftDate,
                Start = start,
                Type = TimeLogType.Break,
                Timezone = user.Timezone,
                CreatedAt = utcNow.UtcDateTime
            };
            db.TimeLogs.Add(breakLog);
            await SaveClockActionAsync("ACTIVE_BREAK_EXISTS", "A break is already active.", ct);
            return TimeLogMapper.ToClockResponse(breakLog, utcNow.UtcDateTime);
        }, cancellationToken);

    public Task<TimeLogResponse> EndBreakAsync(string? dateTime, CancellationToken cancellationToken) =>
        RunClockActionAsync(async (user, utcNow, ct) =>
        {
            var end = DateTimeHelper.ParseUtc(dateTime, utcNow);
            var work = await ActiveWorkAsync(user.Id, ct)
                ?? throw new ConflictException("NO_ACTIVE_WORK_SESSION", "You are not clocked in.");
            var breakLog = work.Breaks.SingleOrDefault(x => !x.IsDeleted && x.End is null)
                ?? throw new ConflictException("NO_ACTIVE_BREAK", "There is no active break to end.");
            if (end <= TimeLogMapper.Utc(breakLog.Start))
                throw new ValidationException("INVALID_BREAK_END", "Break end must be after break start.");

            breakLog.End = end;
            breakLog.UpdatedAt = utcNow.UtcDateTime;
            await db.SaveChangesAsync(ct);
            return TimeLogMapper.ToClockResponse(breakLog, utcNow.UtcDateTime);
        }, cancellationToken);

    public Task<TimeLogResponse> ClockOutAsync(string? dateTime, CancellationToken cancellationToken) =>
        RunClockActionAsync(async (user, utcNow, ct) =>
        {
            var end = DateTimeHelper.ParseUtc(dateTime, utcNow);
            var work = await ActiveWorkAsync(user.Id, ct)
                ?? throw new ConflictException("NO_ACTIVE_WORK_SESSION", "You are not clocked in.");
            if (work.Breaks.Any(x => !x.IsDeleted && x.End is null))
                throw new ConflictException("ACTIVE_BREAK_EXISTS",
                    "End your active break before clocking out.");
            if (end <= TimeLogMapper.Utc(work.Start))
                throw new ValidationException("INVALID_CLOCK_OUT", "Clock-out time must be after clock-in time.");
            if (work.Breaks.Any(x => !x.IsDeleted && x.End > end))
                throw new ValidationException("CLOCK_OUT_BEFORE_BREAK_END",
                    "Clock-out time cannot be earlier than a completed break end.");

            work.End = end;
            work.UpdatedAt = utcNow.UtcDateTime;
            await db.SaveChangesAsync(ct);
            return TimeLogMapper.ToClockResponse(work, utcNow.UtcDateTime);
        }, cancellationToken);

    public async Task<ClockStatusResponse> GetStatusAsync(
        int? requestedUserId,
        CancellationToken cancellationToken)
    {
        var userId = requestedUserId ?? currentUser.UserId;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("USER_NOT_FOUND", "User was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateTimeHelper.LocalDate(now, user.Timezone);
        var works = await db.TimeLogs.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Breaks)
            .Where(x => x.UserId == userId && x.Type == TimeLogType.Work
                && !x.IsDeleted && x.ShiftDate == today)
            .ToListAsync(cancellationToken);
        var activeWork = await db.TimeLogs.AsNoTracking()
            .Include(x => x.Breaks)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Type == TimeLogType.Work
                && !x.IsDeleted && x.End == null, cancellationToken);
        var activeBreak = activeWork?.Breaks.SingleOrDefault(x => !x.IsDeleted && x.End == null);
        var sessions = works.Select(x => TimeLogMapper.ToWorkSession(x, now)).ToList();

        return new(
            activeBreak is not null ? "OnBreak" : activeWork is not null ? "Working" : "ClockedOut",
            activeWork?.Id,
            activeBreak?.Id,
            activeWork is null ? null : TimeLogMapper.Utc(activeWork.Start),
            activeBreak is null ? null : TimeLogMapper.Utc(activeBreak.Start),
            sessions.Sum(x => x.TotalWorkedMinutes),
            sessions.Sum(x => x.TotalBreakMinutes));
    }

    private async Task<TimeLog?> ActiveWorkAsync(int userId, CancellationToken cancellationToken) =>
        await db.TimeLogs
            .Include(x => x.Breaks)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Type == TimeLogType.Work
                && !x.IsDeleted && x.End == null, cancellationToken);

    private async Task<T> RunClockActionAsync<T>(
        Func<User, DateTimeOffset, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var userLock = userLocks.GetLock(userId);
        await userLock.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new UnauthorizedException("USER_NOT_FOUND", "The authenticated user no longer exists.");
            if (!user.IsActive)
                throw new ForbiddenException("USER_INACTIVE", "This account is inactive.");

            var result = await action(user, timeProvider.GetUtcNow(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task SaveClockActionAsync(string code, string message, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(code, message);
        }
    }
}
