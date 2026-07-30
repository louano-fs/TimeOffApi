using TimeOffApi.Contracts;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

internal static class TimeLogMapper
{
    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    public static DateTime? Utc(DateTime? value) => value is null ? null : Utc(value.Value);

    public static TimeLogResponse ToClockResponse(TimeLog log, DateTime utcNow)
    {
        var end = log.End is null ? utcNow : Utc(log.End.Value);
        var worked = log.Type == TimeLogType.Work
            ? end - Utc(log.Start) - TimeSpan.FromMinutes(
                log.Breaks.Where(x => !x.IsDeleted)
                    .Sum(x => DateTimeHelper.Minutes((x.End is null ? utcNow : Utc(x.End.Value)) - Utc(x.Start))))
            : end - Utc(log.Start);

        return new(
            log.Id,
            log.Type.ToString(),
            log.Type == TimeLogType.Break ? (log.End is null ? "OnBreak" : "Completed")
                : log.End is null ? "Working" : "Completed",
            DateOnly.FromDateTime(log.ShiftDate),
            Utc(log.Start),
            Utc(log.End),
            log.Timezone,
            DateTimeHelper.Minutes(worked));
    }

    public static WorkSessionResponse ToWorkSession(TimeLog work, DateTime utcNow)
    {
        var effectiveEnd = work.End is null ? utcNow : Utc(work.End.Value);
        var breaks = work.Breaks.Where(x => !x.IsDeleted).OrderBy(x => x.Start).ToList();
        var breakDtos = breaks.Select(x =>
        {
            var breakEnd = x.End is null ? utcNow : Utc(x.End.Value);
            return new BreakResponse(x.Id, Utc(x.Start), Utc(x.End),
                DateTimeHelper.Minutes(breakEnd - Utc(x.Start)));
        }).ToArray();
        var elapsed = DateTimeHelper.Minutes(effectiveEnd - Utc(work.Start));
        var breakMinutes = breakDtos.Sum(x => x.DurationMinutes);

        return new(
            work.Id,
            work.UserId,
            work.User.EmployeeId,
            DateOnly.FromDateTime(work.ShiftDate),
            Utc(work.Start),
            Utc(work.End),
            work.End is null ? "Active" : "Completed",
            work.Timezone,
            elapsed,
            breakMinutes,
            Math.Max(0, elapsed - breakMinutes),
            breakDtos);
    }
}
