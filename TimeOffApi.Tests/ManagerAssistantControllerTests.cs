using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using TimeOffApi.Contracts;
using TimeOffApi.Controllers;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class ManagerAssistantControllerTests
{
    private static readonly ManagerScope Scope = new(
        80,
        "Asia/Manila",
        new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Message_endpoint_requires_the_manager_role()
    {
        var authorize = typeof(ManagerAssistantController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be(nameof(UserRole.Manager));
    }

    [Fact]
    public async Task Disabled_feature_returns_not_found_after_fresh_scope_validation()
    {
        var scopeResolver = new Mock<IManagerScopeResolver>();
        scopeResolver.Setup(x => x.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scope);
        var orchestrator = new Mock<IManagerAssistantOrchestrator>();
        var controller = CreateController(
            scopeResolver.Object,
            orchestrator.Object,
            Mock.Of<IManagerAssistantRateLimiter>(),
            enabled: false);

        var action = () => controller.SendMessage(
            new("Show team hours."),
            Validator(),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("ASSISTANT_DISABLED");
        scopeResolver.Verify(x => x.ResolveRequiredAsync(It.IsAny<CancellationToken>()), Times.Once);
        orchestrator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Rate_limit_fails_before_the_model_orchestrator()
    {
        var scopeResolver = new Mock<IManagerScopeResolver>();
        scopeResolver.Setup(x => x.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Scope);
        var orchestrator = new Mock<IManagerAssistantOrchestrator>();
        var rateLimiter = new Mock<IManagerAssistantRateLimiter>();
        rateLimiter.Setup(x => x.TryAcquire(Scope.ManagerId)).Returns(false);
        var controller = CreateController(
            scopeResolver.Object,
            orchestrator.Object,
            rateLimiter.Object,
            enabled: true);

        var action = () => controller.SendMessage(
            new("Show team hours."),
            Validator(),
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<TooManyRequestsException>())
            .Which.Code.Should().Be("ASSISTANT_RATE_LIMITED");
        orchestrator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Request_validation_enforces_message_and_history_budgets()
    {
        var validator = Validator();
        var request = new ManagerAssistantMessageRequest(
            new string('x', 1_001),
            Enumerable.Range(0, 9)
                .Select(_ => new ManagerAssistantHistoryMessage(
                    ManagerAssistantHistoryRole.User,
                    "history"))
                .ToArray());

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(request.Message));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(request.History));
    }

    [Fact]
    public void Rate_limiter_allows_ten_messages_per_manager_per_minute()
    {
        var time = new MutableTimeProvider(
            new DateTimeOffset(2026, 8, 6, 4, 0, 0, TimeSpan.Zero));
        var limiter = new ManagerAssistantRateLimiter(time);

        Enumerable.Range(0, 10).Should().OnlyContain(_ => limiter.TryAcquire(80));
        limiter.TryAcquire(80).Should().BeFalse();
        limiter.TryAcquire(81).Should().BeTrue();
        time.Advance(TimeSpan.FromMinutes(1));
        limiter.TryAcquire(80).Should().BeTrue();
    }

    private static ManagerAssistantController CreateController(
        IManagerScopeResolver scopeResolver,
        IManagerAssistantOrchestrator orchestrator,
        IManagerAssistantRateLimiter rateLimiter,
        bool enabled) =>
        new(
            scopeResolver,
            orchestrator,
            rateLimiter,
            Options.Create(new ManagerAssistantOptions
            {
                Enabled = enabled,
                Provider = enabled ? "Fake" : string.Empty,
                Model = enabled ? "fake-tool-model" : string.Empty
            }));

    private static ManagerAssistantMessageRequestValidator Validator() =>
        new(Options.Create(new ManagerAssistantOptions()));

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
