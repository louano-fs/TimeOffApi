using Microsoft.Extensions.Options;
using TimeOffApi.Contracts;

namespace TimeOffApi.Services;

public interface IManagerAssistantCapabilitiesService
{
    Task<ManagerAssistantCapabilitiesResponse> GetAsync(
        CancellationToken cancellationToken);
}

public sealed class ManagerAssistantCapabilitiesService(
    IManagerScopeResolver scopeResolver,
    IOptions<ManagerAssistantOptions> options,
    IAssistantModelAvailability modelAvailability) : IManagerAssistantCapabilitiesService
{
    private readonly ManagerAssistantOptions _options = options.Value;

    public async Task<ManagerAssistantCapabilitiesResponse> GetAsync(
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.TryResolveAsync(cancellationToken);
        var enabled = _options.Enabled && modelAvailability.IsAvailable && scope is not null;

        return new(
            enabled,
            enabled ? "Manager" : null,
            enabled ? "directReports" : null,
            Streaming: false,
            _options.MaxMessageLength);
    }
}
