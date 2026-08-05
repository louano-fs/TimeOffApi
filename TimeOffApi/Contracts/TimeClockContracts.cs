using FluentValidation;

namespace TimeOffApi.Contracts;

public sealed record ClockActionRequest(string? DateTime);

public sealed class ClockActionRequestValidator : AbstractValidator<ClockActionRequest>
{
    public ClockActionRequestValidator()
    {
        RuleFor(x => x.DateTime)
            .NotEmpty().WithMessage("Date and time is required.")
            .Matches(@"(Z|[+-]\d{2}:\d{2})$").WithMessage("Timestamp must include Z or a UTC offset.");
    }
}

public sealed record TimeLogResponse(
    int Id,
    string Type,
    string Status,
    DateOnly ShiftDate,
    DateTime Start,
    DateTime? End,
    string Timezone,
    int WorkedMinutes);

public sealed record ClockStatusResponse(
    string Status,
    int? ActiveWorkLogId,
    int? ActiveBreakLogId,
    DateTime? ClockedInAt,
    DateTime? BreakStartedAt,
    DateTime AsOf,
    DateTime CurrentDayEndsAt,
    int WorkedMinutesToday,
    int BreakMinutesToday,
    int WorkedSecondsToday,
    int BreakSecondsToday);
