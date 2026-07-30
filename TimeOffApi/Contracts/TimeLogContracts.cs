using FluentValidation;

namespace TimeOffApi.Contracts;

public class TimeLogQuery
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class AdminTimeLogQuery : TimeLogQuery
{
    public int? EmployeeId { get; init; }
}

public sealed class SummaryQuery
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

public sealed class TimeLogQueryValidator : AbstractValidator<TimeLogQuery>
{
    public TimeLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.StartDate <= x.EndDate)
            .WithMessage("Start date must not be after end date.");
        RuleFor(x => x.Status)
            .Must(x => x is null || x.Equals("active", StringComparison.OrdinalIgnoreCase)
                || x.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be Active or Completed.");
    }
}

public sealed class AdminTimeLogQueryValidator : AbstractValidator<AdminTimeLogQuery>
{
    public AdminTimeLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.StartDate <= x.EndDate)
            .WithMessage("Start date must not be after end date.");
        RuleFor(x => x.Status)
            .Must(x => x is null || x.Equals("active", StringComparison.OrdinalIgnoreCase)
                || x.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be Active or Completed.");
        RuleFor(x => x.EmployeeId).GreaterThan(0).When(x => x.EmployeeId.HasValue);
    }
}

public sealed class SummaryQueryValidator : AbstractValidator<SummaryQuery>
{
    public SummaryQueryValidator()
    {
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.StartDate <= x.EndDate)
            .WithMessage("Start date must not be after end date.");
    }
}

public sealed record BreakResponse(int Id, DateTime Start, DateTime? End, int DurationMinutes);

public sealed record WorkSessionResponse(
    int Id,
    int UserId,
    int EmployeeId,
    DateOnly ShiftDate,
    DateTime Start,
    DateTime? End,
    string Status,
    string Timezone,
    int TotalElapsedMinutes,
    int TotalBreakMinutes,
    int TotalWorkedMinutes,
    IReadOnlyCollection<BreakResponse> Breaks);

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record DailySummaryResponse(DateOnly Date, int WorkedMinutes, int BreakMinutes);

public sealed record TimeSummaryResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalWorkedMinutes,
    int TotalBreakMinutes,
    int CompletedWorkSessions,
    IReadOnlyCollection<DailySummaryResponse> Daily);
