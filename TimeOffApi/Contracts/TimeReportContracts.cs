using FluentValidation;

namespace TimeOffApi.Contracts;

public class TimeReportQuery
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}

public sealed class TeamTimeReportQuery : TimeReportQuery
{
    public bool IncludeInactive { get; init; }
}

public sealed class TimeReportQueryValidator : AbstractValidator<TimeReportQuery>
{
    public TimeReportQueryValidator()
    {
        RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("Start date must not be after end date.");
        RuleFor(x => x)
            .Must(x => InclusiveDays(x) <= 366)
            .WithMessage("Personal reports may span at most 366 days.");
        RuleFor(x => x.EndDate)
            .NotEqual(DateOnly.MaxValue)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date is outside the supported range.");
        RuleFor(x => x.StartDate)
            .NotEqual(DateOnly.MinValue)
            .When(x => x.StartDate.HasValue)
            .WithMessage("Start date is outside the supported range.");
    }

    internal static bool HaveValidRange(TimeReportQuery query) =>
        query.StartDate is null || query.EndDate is null || query.StartDate <= query.EndDate;

    internal static int InclusiveDays(TimeReportQuery query)
    {
        if (query.StartDate is null || query.EndDate is null || query.StartDate > query.EndDate)
            return 1;

        return query.EndDate.Value.DayNumber - query.StartDate.Value.DayNumber + 1;
    }
}

public sealed class TeamTimeReportQueryValidator : AbstractValidator<TeamTimeReportQuery>
{
    public TeamTimeReportQueryValidator()
    {
        RuleFor(x => x)
            .Must(TimeReportQueryValidator.HaveValidRange)
            .WithMessage("Start date must not be after end date.");
        RuleFor(x => x)
            .Must(x => TimeReportQueryValidator.InclusiveDays(x) <= 92)
            .WithMessage("Team reports may span at most 92 days.");
        RuleFor(x => x.EndDate)
            .NotEqual(DateOnly.MaxValue)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date is outside the supported range.");
        RuleFor(x => x.StartDate)
            .NotEqual(DateOnly.MinValue)
            .When(x => x.StartDate.HasValue)
            .WithMessage("Start date is outside the supported range.");
    }
}

public sealed record DailyTimeReportResponse(
    DateOnly Date,
    int WorkedSeconds,
    int BreakSeconds);

public sealed record PersonalTimeReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    int WorkedSeconds,
    int BreakSeconds,
    int WorkSessionCount,
    IReadOnlyCollection<DailyTimeReportResponse> Daily);

public sealed record TeamMemberTimeReportResponse(
    int UserId,
    int EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    bool IsActive,
    int WorkedSeconds,
    int BreakSeconds,
    int WorkSessionCount);

public sealed record TeamTimeReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    int TotalWorkedSeconds,
    int TotalBreakSeconds,
    double? AverageWorkedSeconds,
    IReadOnlyCollection<TeamMemberTimeReportResponse> Members);
