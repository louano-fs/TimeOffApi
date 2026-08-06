using System.Text.Json.Serialization;

namespace TimeOffApi.Services;

public enum TeamWorkedTimeOrder
{
    [JsonStringEnumMemberName("name")]
    Name,
    [JsonStringEnumMemberName("workedAscending")]
    WorkedAscending,
    [JsonStringEnumMemberName("workedDescending")]
    WorkedDescending
}

public enum WorkedTimeComparison
{
    [JsonStringEnumMemberName("lessThan")]
    LessThan,
    [JsonStringEnumMemberName("lessThanOrEqual")]
    LessThanOrEqual,
    [JsonStringEnumMemberName("greaterThan")]
    GreaterThan,
    [JsonStringEnumMemberName("greaterThanOrEqual")]
    GreaterThanOrEqual
}

public enum WorkedTimeUnit
{
    [JsonStringEnumMemberName("seconds")]
    Seconds,
    [JsonStringEnumMemberName("minutes")]
    Minutes,
    [JsonStringEnumMemberName("hours")]
    Hours
}

public enum TeamClockStatus
{
    Working,
    OnBreak,
    ClockedOut
}

public sealed record TeamWorkedTimeArguments(
    DateOnly StartDate,
    DateOnly EndDate,
    bool IncludeInactive = false,
    TeamWorkedTimeOrder Order = TeamWorkedTimeOrder.Name,
    int? Limit = null);

public sealed record TeamWorkedTimeThresholdArguments(
    DateOnly StartDate,
    DateOnly EndDate,
    WorkedTimeComparison Comparison,
    decimal ThresholdValue,
    WorkedTimeUnit ThresholdUnit,
    bool IncludeInactive = false);

public sealed record DirectReportWorkedTimeArguments(
    string EmployeeReference,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IncludeInactive = false);

public sealed record TeamCurrentStatusArguments(bool IncludeInactive = false);

public sealed record TeamTimeLogExportArguments(
    DateOnly StartDate,
    DateOnly EndDate,
    bool IncludeInactive = false);

public sealed record TeamWorkedTimeEvidence(
    string EmployeeNumber,
    string DisplayName,
    bool IsActive,
    int WorkedSeconds,
    int BreakSeconds,
    TeamClockStatus ClockStatus,
    int? Rank);

public sealed record TeamStatusEvidence(
    string EmployeeNumber,
    string DisplayName,
    bool IsActive,
    TeamClockStatus ClockStatus);

public abstract record ManagerAssistantToolResult(string PartType);

public sealed record TeamWorkedTimeToolResult(
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
    : ManagerAssistantToolResult("teamWorkedTimeSummary");

public sealed record TeamWorkedTimeThresholdToolResult(
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
    : ManagerAssistantToolResult("teamWorkedTimeThreshold");

public sealed record DirectReportWorkedTimeToolResult(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool PeriodComplete,
    TeamWorkedTimeEvidence Member)
    : ManagerAssistantToolResult("directReportWorkedTime");

public sealed record TeamCurrentStatusToolResult(
    string ReportingTimezone,
    DateTime AsOf,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    IReadOnlyCollection<TeamStatusEvidence> Members)
    : ManagerAssistantToolResult("teamCurrentStatus");

public sealed record TeamTimeLogExportToolResult(
    DateOnly StartDate,
    DateOnly EndDate,
    string ReportingTimezone,
    DateTime AsOf,
    bool IncludeInactive,
    int IncludedMemberCount,
    int ExcludedInactiveCount,
    string FileName,
    string DownloadUrl)
    : ManagerAssistantToolResult("teamTimeLogExport");

public sealed record TeamMemberClarificationToolResult(
    IReadOnlyCollection<DirectReportCandidate> Candidates)
    : ManagerAssistantToolResult("teamMemberClarification");
