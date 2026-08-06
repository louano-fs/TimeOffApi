using FluentValidation;

namespace TimeOffApi.Contracts;

public class TimeLogExportQuery
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string Format { get; init; } = "xlsx";
}

public sealed class TeamTimeLogExportQuery : TimeLogExportQuery
{
    public bool IncludeInactive { get; init; }
}

public sealed class TimeLogExportQueryValidator : AbstractValidator<TimeLogExportQuery>
{
    public TimeLogExportQueryValidator()
    {
        IncludeCommonRules(this);
    }

    internal static void IncludeCommonRules<T>(AbstractValidator<T> validator)
        where T : TimeLogExportQuery
    {
        validator.RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("Start date must not be after end date.");
        validator.RuleFor(x => x.EndDate)
            .NotEqual(DateOnly.MaxValue)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date is outside the supported range.");
        validator.RuleFor(x => x.StartDate)
            .NotEqual(DateOnly.MinValue)
            .When(x => x.StartDate.HasValue)
            .WithMessage("Start date is outside the supported range.");
        validator.RuleFor(x => x.Format)
            .NotEmpty()
            .Must(x => string.Equals(x, "xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Format must be xlsx.");
    }

    internal static bool HaveValidRange(TimeLogExportQuery query) =>
        query.StartDate is null || query.EndDate is null || query.StartDate <= query.EndDate;
}

public sealed class TeamTimeLogExportQueryValidator : AbstractValidator<TeamTimeLogExportQuery>
{
    public TeamTimeLogExportQueryValidator()
    {
        TimeLogExportQueryValidator.IncludeCommonRules(this);
    }
}

public sealed record TimeLogExportFile(
    byte[] Contents,
    string ContentType,
    string FileName);
