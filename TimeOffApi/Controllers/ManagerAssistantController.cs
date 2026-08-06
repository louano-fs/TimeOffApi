using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TimeOffApi.Contracts;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/manager-assistant")]
[Authorize(Roles = nameof(UserRole.Manager))]
public sealed class ManagerAssistantController(
    IManagerScopeResolver scopeResolver,
    IManagerAssistantOrchestrator orchestrator,
    IManagerAssistantRateLimiter rateLimiter,
    IOptions<ManagerAssistantOptions> options) : ControllerBase
{
    private readonly ManagerAssistantOptions _options = options.Value;

    [HttpPost("messages")]
    public async Task<ManagerAssistantMessageResponse> SendMessage(
        ManagerAssistantMessageRequest request,
        [FromServices] IValidator<ManagerAssistantMessageRequest> validator,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveRequiredAsync(cancellationToken);
        if (!_options.Enabled)
            throw new NotFoundException(
                "ASSISTANT_DISABLED",
                "The team assistant is not enabled.");

        await validator.ValidateOrThrowAsync(request, cancellationToken);
        if (!rateLimiter.TryAcquire(scope.ManagerId))
            throw new TooManyRequestsException(
                "ASSISTANT_RATE_LIMITED",
                "The team assistant message limit has been reached. Please try again shortly.");

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        try
        {
            return await orchestrator.SendAsync(scope, request, requestTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(
                "ASSISTANT_UNAVAILABLE",
                "The team assistant is temporarily unavailable.");
        }
    }
}
