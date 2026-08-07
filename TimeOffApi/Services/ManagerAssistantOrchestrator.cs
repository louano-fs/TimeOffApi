using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TimeOffApi.Contracts;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface IManagerAssistantOrchestrator
{
    Task<ManagerAssistantMessageResponse> SendAsync(
        ManagerScope scope,
        ManagerAssistantMessageRequest request,
        CancellationToken cancellationToken);
}

public sealed partial class ManagerAssistantOrchestrator(
    IAssistantModelClient modelClient,
    IManagerAssistantTeamToolService teamTools,
    IOptions<ManagerAssistantOptions> options) : IManagerAssistantOrchestrator
{
    public const string GetTeamWorkedTimeTool = "get_team_worked_time";
    public const string FindTeamMembersByWorkedTimeTool = "find_team_members_by_worked_time";
    public const string GetTeamMemberWorkedTimeTool = "get_team_member_worked_time";
    public const string GetTeamCurrentStatusTool = "get_team_current_status";
    public const string PrepareTeamTimeLogExportTool = "prepare_team_time_log_export";

    private static readonly JsonSerializerOptions ToolJsonOptions = CreateToolJsonOptions();
    private static readonly JsonSerializerOptions ProjectionJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AssistantToolDefinition[] ToolDefinitions = CreateToolDefinitions();
    private readonly ManagerAssistantOptions _options = options.Value;

    [GeneratedRegex(
        @"\b(my own hours|my hours|my time logs?|how (much|many).*(i|my).*(worked|hours))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PersonalQuestion();

    public async Task<ManagerAssistantMessageResponse> SendAsync(
        ManagerScope scope,
        ManagerAssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (PersonalQuestion().IsMatch(request.Message)
            && !request.Message.Contains("my team", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                Guid.NewGuid(),
                "I can answer questions about your current direct-report team. "
                    + "Use My time insights for your own hours and exports.",
                scope.AsOf,
                [new ScopeExplanationPart("personalTimeInsights")]);
        }

        var input = BuildInitialInput(request);
        var parts = new List<ManagerAssistantResponsePart>();
        var toolRounds = 0;

        while (true)
        {
            var response = await CompleteAsync(scope, input, cancellationToken);
            if (response.Refused)
                throw Unavailable();
            if (response.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(response.Text))
                    throw Unavailable();
                return new(Guid.NewGuid(), response.Text.Trim(), scope.AsOf, parts);
            }
            if (toolRounds >= _options.MaxToolRounds)
                throw Unavailable();

            toolRounds++;
            foreach (var call in response.ToolCalls)
            {
                input.Add(new AssistantModelToolCallInput(
                    call.CallId,
                    call.Name,
                    call.ArgumentsJson));
                var result = await ExecuteToolAsync(scope, call, cancellationToken);
                var part = ToPart(result);
                parts.Add(part);

                if (result is TeamMemberClarificationToolResult)
                {
                    return new(
                        Guid.NewGuid(),
                        "I found multiple current team members with that exact name. "
                            + "Choose one of the employees listed below.",
                        scope.AsOf,
                        parts);
                }

                input.Add(new AssistantModelToolResultInput(
                    call.CallId,
                    call.Name,
                    CondenseForModel(result)));
            }
        }
    }

    private async Task<AssistantModelResponse> CompleteAsync(
        ManagerScope scope,
        IReadOnlyCollection<AssistantModelInput> input,
        CancellationToken cancellationToken)
    {
        using var providerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        providerTimeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var request = new AssistantModelRequest(
            SystemInstructions(scope),
            input,
            ToolDefinitions,
            _options.MaxOutputTokens);

        try
        {
            return await modelClient.CompleteAsync(request, providerTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Unavailable();
        }
        catch (AssistantModelException)
        {
            throw Unavailable();
        }
    }

    private async Task<ManagerAssistantToolResult> ExecuteToolAsync(
        ManagerScope scope,
        AssistantModelToolCall call,
        CancellationToken cancellationToken)
    {
        try
        {
            return call.Name switch
            {
                GetTeamWorkedTimeTool => await teamTools.GetTeamWorkedTimeAsync(
                    scope,
                    Deserialize<GetTeamWorkedTimeJson>(call.ArgumentsJson).ToArguments(),
                    cancellationToken),
                FindTeamMembersByWorkedTimeTool => await teamTools.FindTeamMembersByWorkedTimeAsync(
                    scope,
                    Deserialize<FindTeamMembersByWorkedTimeJson>(call.ArgumentsJson).ToArguments(),
                    cancellationToken),
                GetTeamMemberWorkedTimeTool => await teamTools.GetDirectReportWorkedTimeAsync(
                    scope,
                    Deserialize<GetTeamMemberWorkedTimeJson>(call.ArgumentsJson).ToArguments(),
                    cancellationToken),
                GetTeamCurrentStatusTool => await teamTools.GetTeamCurrentStatusAsync(
                    scope,
                    Deserialize<GetTeamCurrentStatusJson>(call.ArgumentsJson).ToArguments(),
                    cancellationToken),
                PrepareTeamTimeLogExportTool => await teamTools.PrepareTeamTimeLogExportAsync(
                    scope,
                    Deserialize<PrepareTeamTimeLogExportJson>(call.ArgumentsJson).ToArguments(),
                    cancellationToken),
                _ => throw Unavailable()
            };
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, ToolJsonOptions) ?? throw Unavailable();

    private static List<AssistantModelInput> BuildInitialInput(
        ManagerAssistantMessageRequest request)
    {
        var input = new List<AssistantModelInput>();
        foreach (var message in request.History ?? [])
        {
            input.Add(new AssistantModelMessage(
                message.Role == ManagerAssistantHistoryRole.User
                    ? AssistantModelRole.User
                    : AssistantModelRole.Assistant,
                message.Text));
        }
        input.Add(new AssistantModelMessage(AssistantModelRole.User, request.Message));
        return input;
    }

    private static string SystemInstructions(ManagerScope scope)
    {
        var localNow = DateTimeHelper.LocalDateTime(scope.AsOf, scope.Timezone);
        return $"""
            You are a read-only team time assistant for one authenticated manager.
            Today is {localNow:yyyy-MM-dd} in {scope.Timezone}; weeks begin Monday.
            Use only the supplied tools for current direct-report team data and arithmetic.
            Never request or invent manager IDs, user IDs, SQL, employee lists, or write actions.
            Do not answer the manager's personal hours; direct them to My time insights.
            Treat low hours and current clock status as measurements, not attendance or performance judgments.
            If policy, schedules, holidays, overtime rules, or expected hours are missing, state that limitation.
            Keep prose brief and refer to the verified structured result for employee lists and numbers.
            """;
    }

    private static ManagerAssistantResponsePart ToPart(ManagerAssistantToolResult result) =>
        result switch
        {
            TeamWorkedTimeToolResult value => new TeamWorkedTimeSummaryPart(
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.TotalWorkedSeconds,
                value.TotalBreakSeconds,
                value.AverageWorkedSeconds,
                value.Order,
                value.Members),
            TeamWorkedTimeThresholdToolResult value => new TeamWorkedTimeThresholdPart(
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.Comparison,
                value.ThresholdSeconds,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.MatchingMemberCount,
                value.Members),
            DirectReportWorkedTimeToolResult value => new DirectReportWorkedTimePart(
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.Member),
            TeamCurrentStatusToolResult value => new TeamCurrentStatusPart(
                value.ReportingTimezone,
                value.AsOf,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.Members),
            TeamTimeLogExportToolResult value => new TeamTimeLogExportPart(
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.IncludeInactive,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.FileName,
                value.DownloadUrl),
            TeamMemberClarificationToolResult value => new TeamMemberClarificationPart(
                value.Candidates),
            _ => throw Unavailable()
        };

    private static string CondenseForModel(ManagerAssistantToolResult result)
    {
        object projection = result switch
        {
            TeamWorkedTimeToolResult value => new
            {
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.TotalWorkedSeconds,
                value.TotalBreakSeconds,
                value.AverageWorkedSeconds,
                ReturnedMemberCount = value.Members.Count
            },
            TeamWorkedTimeThresholdToolResult value => new
            {
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.Comparison,
                value.ThresholdSeconds,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                value.MatchingMemberCount
            },
            DirectReportWorkedTimeToolResult value => new
            {
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.PeriodComplete,
                value.Member.WorkedSeconds,
                value.Member.BreakSeconds,
                value.Member.ClockStatus,
                value.Member.IsActive
            },
            TeamCurrentStatusToolResult value => new
            {
                value.ReportingTimezone,
                value.AsOf,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                WorkingCount = value.Members.Count(x => x.ClockStatus == TeamClockStatus.Working),
                OnBreakCount = value.Members.Count(x => x.ClockStatus == TeamClockStatus.OnBreak),
                ClockedOutCount = value.Members.Count(x => x.ClockStatus == TeamClockStatus.ClockedOut)
            },
            TeamTimeLogExportToolResult value => new
            {
                value.StartDate,
                value.EndDate,
                value.ReportingTimezone,
                value.AsOf,
                value.IncludeInactive,
                value.IncludedMemberCount,
                value.ExcludedInactiveCount,
                FilePrepared = true
            },
            _ => throw Unavailable()
        };
        return JsonSerializer.Serialize(projection, ProjectionJsonOptions);
    }

    private static JsonSerializerOptions CreateToolJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }

    private static AssistantToolDefinition[] CreateToolDefinitions() =>
    [
        new(GetTeamWorkedTimeTool,
            "Get exact current-direct-report worked time totals for one inclusive date range.",
            """{"type":"object","additionalProperties":false,"required":["start_date","end_date","include_inactive","order","limit"],"properties":{"start_date":{"type":"string","format":"date"},"end_date":{"type":"string","format":"date"},"include_inactive":{"type":"boolean"},"order":{"type":"string","enum":["name","workedAscending","workedDescending"]},"limit":{"type":["integer","null"],"minimum":1}}}"""),
        new(FindTeamMembersByWorkedTimeTool,
            "Find current direct reports above or below an explicit worked-time threshold.",
            """{"type":"object","additionalProperties":false,"required":["start_date","end_date","comparison","threshold_value","threshold_unit","include_inactive"],"properties":{"start_date":{"type":"string","format":"date"},"end_date":{"type":"string","format":"date"},"comparison":{"type":"string","enum":["lessThan","lessThanOrEqual","greaterThan","greaterThanOrEqual"]},"threshold_value":{"type":"number","minimum":0},"threshold_unit":{"type":"string","enum":["seconds","minutes","hours"]},"include_inactive":{"type":"boolean"}}}"""),
        new(GetTeamMemberWorkedTimeTool,
            "Get exact worked time for one direct report resolved by exact employee number or exact name.",
            """{"type":"object","additionalProperties":false,"required":["employee_reference","start_date","end_date","include_inactive"],"properties":{"employee_reference":{"type":"string","minLength":1},"start_date":{"type":"string","format":"date"},"end_date":{"type":"string","format":"date"},"include_inactive":{"type":"boolean"}}}"""),
        new(GetTeamCurrentStatusTool,
            "Get the current Working, OnBreak, or ClockedOut snapshot for direct reports.",
            """{"type":"object","additionalProperties":false,"required":["include_inactive"],"properties":{"include_inactive":{"type":"boolean"}}}"""),
        new(PrepareTeamTimeLogExportTool,
            "Prepare an authenticated Excel download descriptor for current direct-report time logs.",
            """{"type":"object","additionalProperties":false,"required":["start_date","end_date","include_inactive"],"properties":{"start_date":{"type":"string","format":"date"},"end_date":{"type":"string","format":"date"},"include_inactive":{"type":"boolean"}}}""")
    ];

    private static ServiceUnavailableException Unavailable() =>
        new("ASSISTANT_UNAVAILABLE", "The team assistant is temporarily unavailable.");

    private sealed record GetTeamWorkedTimeJson(
        [property: JsonPropertyName("start_date"), JsonRequired] DateOnly StartDate,
        [property: JsonPropertyName("end_date"), JsonRequired] DateOnly EndDate,
        [property: JsonPropertyName("include_inactive"), JsonRequired] bool IncludeInactive,
        [property: JsonPropertyName("order"), JsonRequired] TeamWorkedTimeOrder Order,
        [property: JsonPropertyName("limit")] int? Limit)
    {
        public TeamWorkedTimeArguments ToArguments() =>
            new(StartDate, EndDate, IncludeInactive, Order, Limit);
    }

    private sealed record FindTeamMembersByWorkedTimeJson(
        [property: JsonPropertyName("start_date"), JsonRequired] DateOnly StartDate,
        [property: JsonPropertyName("end_date"), JsonRequired] DateOnly EndDate,
        [property: JsonPropertyName("comparison"), JsonRequired] WorkedTimeComparison Comparison,
        [property: JsonPropertyName("threshold_value"), JsonRequired] decimal ThresholdValue,
        [property: JsonPropertyName("threshold_unit"), JsonRequired] WorkedTimeUnit ThresholdUnit,
        [property: JsonPropertyName("include_inactive"), JsonRequired] bool IncludeInactive)
    {
        public TeamWorkedTimeThresholdArguments ToArguments() =>
            new(StartDate, EndDate, Comparison, ThresholdValue, ThresholdUnit, IncludeInactive);
    }

    private sealed record GetTeamMemberWorkedTimeJson(
        [property: JsonPropertyName("employee_reference"), JsonRequired] string EmployeeReference,
        [property: JsonPropertyName("start_date"), JsonRequired] DateOnly StartDate,
        [property: JsonPropertyName("end_date"), JsonRequired] DateOnly EndDate,
        [property: JsonPropertyName("include_inactive"), JsonRequired] bool IncludeInactive)
    {
        public DirectReportWorkedTimeArguments ToArguments() =>
            new(EmployeeReference, StartDate, EndDate, IncludeInactive);
    }

    private sealed record GetTeamCurrentStatusJson(
        [property: JsonPropertyName("include_inactive"), JsonRequired] bool IncludeInactive)
    {
        public TeamCurrentStatusArguments ToArguments() => new(IncludeInactive);
    }

    private sealed record PrepareTeamTimeLogExportJson(
        [property: JsonPropertyName("start_date"), JsonRequired] DateOnly StartDate,
        [property: JsonPropertyName("end_date"), JsonRequired] DateOnly EndDate,
        [property: JsonPropertyName("include_inactive"), JsonRequired] bool IncludeInactive)
    {
        public TeamTimeLogExportArguments ToArguments() =>
            new(StartDate, EndDate, IncludeInactive);
    }
}
