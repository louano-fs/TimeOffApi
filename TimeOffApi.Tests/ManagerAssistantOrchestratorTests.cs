using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TimeOffApi.Contracts;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class ManagerAssistantOrchestratorTests
{
    private static readonly ManagerScope Scope = new(
        80,
        "Asia/Manila",
        new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Exposes_only_the_five_read_only_team_tools_and_returns_verified_parts()
    {
        var model = new ScriptedModelClient(
            new AssistantModelResponse(
                null,
                [ToolCall(
                    ManagerAssistantOrchestrator.GetTeamWorkedTimeTool,
                    """{"start_date":"2026-08-03","end_date":"2026-08-06","include_inactive":false,"order":"name","limit":null}""")]),
            new AssistantModelResponse("I found the current team totals.", []));
        var tools = new Mock<IManagerAssistantTeamToolService>();
        tools.Setup(x => x.GetTeamWorkedTimeAsync(
                Scope,
                It.IsAny<TeamWorkedTimeArguments>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SummaryResult());
        var orchestrator = Create(model, tools.Object);

        var result = await orchestrator.SendAsync(
            Scope,
            new("Show team hours this week."),
            TestContext.Current.CancellationToken);

        model.Requests[0].Tools.Select(x => x.Name).Should().Equal(
            ManagerAssistantOrchestrator.GetTeamWorkedTimeTool,
            ManagerAssistantOrchestrator.FindTeamMembersByWorkedTimeTool,
            ManagerAssistantOrchestrator.GetTeamMemberWorkedTimeTool,
            ManagerAssistantOrchestrator.GetTeamCurrentStatusTool,
            ManagerAssistantOrchestrator.PrepareTeamTimeLogExportTool);
        model.Requests[0].Tools.Should().OnlyContain(x =>
            !x.JsonSchema.Contains("manager_id", StringComparison.OrdinalIgnoreCase)
            && !x.JsonSchema.Contains("user_id", StringComparison.OrdinalIgnoreCase)
            && !x.Name.Contains("write", StringComparison.OrdinalIgnoreCase));
        model.Requests[0].Tools.Should().OnlyContain(x => HasStrictObjectSchema(x));
        result.Answer.Should().Be("I found the current team totals.");
        result.Parts.Should().ContainSingle().Which.Should().BeOfType<TeamWorkedTimeSummaryPart>();
    }

    private static bool HasStrictObjectSchema(AssistantToolDefinition tool)
    {
        using var schema = JsonDocument.Parse(tool.JsonSchema);
        var root = schema.RootElement;
        var properties = root.GetProperty("properties")
            .EnumerateObject()
            .Select(x => x.Name)
            .Order()
            .ToArray();
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Order()
            .ToArray();

        return root.GetProperty("additionalProperties").ValueKind == JsonValueKind.False
            && properties.SequenceEqual(required);
    }

    [Fact]
    public async Task Multi_member_model_projection_omits_employee_identifiers()
    {
        var model = new ScriptedModelClient(
            new AssistantModelResponse(
                null,
                [ToolCall(
                    ManagerAssistantOrchestrator.GetTeamWorkedTimeTool,
                    """{"start_date":"2026-08-06","end_date":"2026-08-06","include_inactive":false,"order":"workedDescending","limit":null}""")]),
            new AssistantModelResponse("The verified table contains the team breakdown.", []));
        var tools = new Mock<IManagerAssistantTeamToolService>();
        tools.Setup(x => x.GetTeamWorkedTimeAsync(
                Scope,
                It.IsAny<TeamWorkedTimeArguments>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SummaryResult());
        var orchestrator = Create(model, tools.Object);

        await orchestrator.SendAsync(
            Scope,
            new("Rank the team."),
            TestContext.Current.CancellationToken);

        var projection = model.Requests[1].Input
            .OfType<AssistantModelToolResultInput>()
            .Single()
            .ResultJson;
        projection.Should().NotContain("Ada Active");
        projection.Should().NotContain("EMP-1001");
        projection.Should().NotContain("employeeNumber");
        projection.Should().Contain("totalWorkedSeconds");
    }

    [Fact]
    public async Task Personal_questions_short_circuit_without_a_model_or_team_tool()
    {
        var model = new ScriptedModelClient();
        var tools = new Mock<IManagerAssistantTeamToolService>();
        var orchestrator = Create(model, tools.Object);

        var result = await orchestrator.SendAsync(
            Scope,
            new("How much hours have I worked today?"),
            TestContext.Current.CancellationToken);

        result.Parts.Should().ContainSingle().Which.Should().BeOfType<ScopeExplanationPart>();
        model.Requests.Should().BeEmpty();
        tools.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Clarification_stops_before_names_are_returned_to_the_model()
    {
        var model = new ScriptedModelClient(
            new AssistantModelResponse(
                null,
                [ToolCall(
                    ManagerAssistantOrchestrator.GetTeamMemberWorkedTimeTool,
                    """{"employee_reference":"Sam","start_date":"2026-08-06","end_date":"2026-08-06","include_inactive":false}""")]));
        var tools = new Mock<IManagerAssistantTeamToolService>();
        tools.Setup(x => x.GetDirectReportWorkedTimeAsync(
                Scope,
                It.IsAny<DirectReportWorkedTimeArguments>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamMemberClarificationToolResult(
            [
                new("EMP-1002", "Sam First"),
                new("EMP-1003", "Sam Second")
            ]));
        var orchestrator = Create(model, tools.Object);

        var result = await orchestrator.SendAsync(
            Scope,
            new("How much did Sam work?"),
            TestContext.Current.CancellationToken);

        result.Parts.Should().ContainSingle().Which.Should()
            .BeOfType<TeamMemberClarificationPart>();
        model.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("unknown_tool", "{}")]
    [InlineData(
        ManagerAssistantOrchestrator.GetTeamCurrentStatusTool,
        "{\"include_inactive\":false,\"manager_id\":80}")]
    [InlineData(ManagerAssistantOrchestrator.GetTeamCurrentStatusTool, "{}")]
    public async Task Unknown_tools_and_unexpected_arguments_fail_closed(
        string toolName,
        string argumentsJson)
    {
        var model = new ScriptedModelClient(
            new AssistantModelResponse(null, [ToolCall(toolName, argumentsJson)]));
        var orchestrator = Create(model, Mock.Of<IManagerAssistantTeamToolService>());

        var action = () => orchestrator.SendAsync(
            Scope,
            new("Show the team."),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ServiceUnavailableException>();
        exception.Which.Code.Should().Be("ASSISTANT_UNAVAILABLE");
    }

    [Fact]
    public async Task Stops_after_the_configured_maximum_tool_rounds()
    {
        var response = new AssistantModelResponse(
            null,
            [ToolCall(
                ManagerAssistantOrchestrator.GetTeamCurrentStatusTool,
                """{"include_inactive":false}""")]);
        var model = new ScriptedModelClient(response, response, response, response);
        var tools = new Mock<IManagerAssistantTeamToolService>();
        tools.Setup(x => x.GetTeamCurrentStatusAsync(
                Scope,
                It.IsAny<TeamCurrentStatusArguments>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamCurrentStatusToolResult(
                Scope.Timezone,
                Scope.AsOf,
                0,
                0,
                []));
        var orchestrator = Create(model, tools.Object);

        var action = () => orchestrator.SendAsync(
            Scope,
            new("Keep checking."),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<ServiceUnavailableException>())
            .Which.Code.Should().Be("ASSISTANT_UNAVAILABLE");
        model.Requests.Should().HaveCount(4);
        tools.Verify(x => x.GetTeamCurrentStatusAsync(
            Scope,
            It.IsAny<TeamCurrentStatusArguments>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Provider_failures_use_the_safe_unavailable_error()
    {
        var model = new ThrowingModelClient();
        var orchestrator = Create(model, Mock.Of<IManagerAssistantTeamToolService>());

        var action = () => orchestrator.SendAsync(
            Scope,
            new("Show team hours."),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<ServiceUnavailableException>())
            .Which.Code.Should().Be("ASSISTANT_UNAVAILABLE");
    }

    [Fact]
    public async Task Failed_tools_discard_model_prose_that_depends_on_them()
    {
        var model = new ScriptedModelClient(
            new AssistantModelResponse(
                "Ada worked eight hours.",
                [ToolCall(
                    ManagerAssistantOrchestrator.GetTeamMemberWorkedTimeTool,
                    """{"employee_reference":"Ada","start_date":"2026-08-06","end_date":"2026-08-06","include_inactive":false}""")]));
        var tools = new Mock<IManagerAssistantTeamToolService>();
        tools.Setup(x => x.GetDirectReportWorkedTimeAsync(
                Scope,
                It.IsAny<DirectReportWorkedTimeArguments>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(
                "TEAM_MEMBER_NOT_FOUND",
                "The requested team member was not found."));
        var orchestrator = Create(model, tools.Object);

        var action = () => orchestrator.SendAsync(
            Scope,
            new("How much did Ada work?"),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("TEAM_MEMBER_NOT_FOUND");
        model.Requests.Should().ContainSingle();
    }

    [Fact]
    public void Structured_parts_serialize_with_a_stable_type_discriminator()
    {
        var response = new ManagerAssistantMessageResponse(
            Guid.NewGuid(),
            "Use personal insights.",
            Scope.AsOf,
            [new ScopeExplanationPart("personalTimeInsights")]);

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("\"type\":\"scopeExplanation\"");
        json.Should().Contain("\"Destination\":\"personalTimeInsights\"");
    }

    private static ManagerAssistantOrchestrator Create(
        IAssistantModelClient model,
        IManagerAssistantTeamToolService tools) =>
        new(
            model,
            tools,
            Options.Create(new ManagerAssistantOptions
            {
                Enabled = true,
                Provider = "Fake",
                Model = "fake-tool-model"
            }));

    private static AssistantModelToolCall ToolCall(string name, string argumentsJson) =>
        new(Guid.NewGuid().ToString("N"), name, argumentsJson);

    private static TeamWorkedTimeToolResult SummaryResult() =>
        new(
            new DateOnly(2026, 8, 6),
            new DateOnly(2026, 8, 6),
            Scope.Timezone,
            Scope.AsOf,
            PeriodComplete: false,
            IncludedMemberCount: 1,
            ExcludedInactiveCount: 0,
            TotalWorkedSeconds: 28_800,
            TotalBreakSeconds: 0,
            AverageWorkedSeconds: 28_800,
            TeamWorkedTimeOrder.Name,
            [new(
                "EMP-1001",
                "Ada Active",
                IsActive: true,
                WorkedSeconds: 28_800,
                BreakSeconds: 0,
                TeamClockStatus.ClockedOut,
                Rank: null)]);

    private sealed class ScriptedModelClient(params AssistantModelResponse[] responses)
        : IAssistantModelClient
    {
        private readonly Queue<AssistantModelResponse> _responses = new(responses);
        public List<AssistantModelRequest> Requests { get; } = [];

        public Task<AssistantModelResponse> CompleteAsync(
            AssistantModelRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request with { Input = request.Input.ToArray() });
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ThrowingModelClient : IAssistantModelClient
    {
        public Task<AssistantModelResponse> CompleteAsync(
            AssistantModelRequest request,
            CancellationToken cancellationToken) =>
            throw new AssistantModelException("provider failed");
    }
}
