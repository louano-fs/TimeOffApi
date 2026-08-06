using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.Extensions.Options;
using TimeOffApi.Services;

namespace TimeOffApi.Contracts;

public enum ManagerAssistantHistoryRole
{
    [JsonStringEnumMemberName("user")]
    User,
    [JsonStringEnumMemberName("assistant")]
    Assistant
}

public sealed record ManagerAssistantHistoryMessage(
    ManagerAssistantHistoryRole Role,
    string Text);

public sealed record ManagerAssistantMessageRequest(
    string Message,
    IReadOnlyCollection<ManagerAssistantHistoryMessage>? History = null);

public sealed class ManagerAssistantMessageRequestValidator
    : AbstractValidator<ManagerAssistantMessageRequest>
{
    public ManagerAssistantMessageRequestValidator(IOptions<ManagerAssistantOptions> options)
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(options.Value.MaxMessageLength);
        RuleFor(x => x.History)
            .Must(x => x is null || x.Count <= 8)
            .WithMessage("At most eight prior messages may be supplied.");
        RuleForEach(x => x.History)
            .ChildRules(message =>
            {
                message.RuleFor(x => x.Text).NotEmpty()
                    .MaximumLength(options.Value.MaxMessageLength);
                message.RuleFor(x => x.Role).IsInEnum();
            })
            .When(x => x.History is not null);
    }
}

public sealed record ManagerAssistantMessageResponse(
    Guid MessageId,
    string Answer,
    DateTime AsOf,
    IReadOnlyCollection<ManagerAssistantResponsePart> Parts);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TeamWorkedTimeSummaryPart), "teamWorkedTimeSummary")]
[JsonDerivedType(typeof(TeamWorkedTimeThresholdPart), "teamWorkedTimeThreshold")]
[JsonDerivedType(typeof(DirectReportWorkedTimePart), "directReportWorkedTime")]
[JsonDerivedType(typeof(TeamCurrentStatusPart), "teamCurrentStatus")]
[JsonDerivedType(typeof(TeamTimeLogExportPart), "teamTimeLogExport")]
[JsonDerivedType(typeof(ScopeExplanationPart), "scopeExplanation")]
[JsonDerivedType(typeof(TeamMemberClarificationPart), "teamMemberClarification")]
public abstract record ManagerAssistantResponsePart;

public sealed record TeamWorkedTimeSummaryPart(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool PeriodComplete,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    int TotalWorkedSeconds,
    int TotalBreakSeconds,
    double? AverageWorkedSeconds,
    TeamWorkedTimeOrder Order,
    IReadOnlyCollection<TeamWorkedTimeEvidence> Members)
    : ManagerAssistantResponsePart;

public sealed record TeamWorkedTimeThresholdPart(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool PeriodComplete,
    WorkedTimeComparison Comparison,
    int ThresholdSeconds,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    int MatchingMemberCount,
    IReadOnlyCollection<TeamWorkedTimeEvidence> Members)
    : ManagerAssistantResponsePart;

public sealed record DirectReportWorkedTimePart(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool PeriodComplete,
    TeamWorkedTimeEvidence Member)
    : ManagerAssistantResponsePart;

public sealed record TeamCurrentStatusPart(
    string ReportingTimezone,
    DateTime AsOf,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    IReadOnlyCollection<TeamStatusEvidence> Members)
    : ManagerAssistantResponsePart;

public sealed record TeamTimeLogExportPart(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool IncludeInactive,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    string FileName,
    string DownloadUrl)
    : ManagerAssistantResponsePart;

public sealed record ScopeExplanationPart(string Destination)
    : ManagerAssistantResponsePart;

public sealed record TeamMemberClarificationPart(
    IReadOnlyCollection<DirectReportCandidate> Candidates)
    : ManagerAssistantResponsePart;
