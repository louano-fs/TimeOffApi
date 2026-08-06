using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeOffApi.Contracts;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/manager-assistant/capabilities")]
[Authorize]
public sealed class ManagerAssistantCapabilitiesController(
    IManagerAssistantCapabilitiesService capabilities) : ControllerBase
{
    [HttpGet]
    public Task<ManagerAssistantCapabilitiesResponse> Get(
        CancellationToken cancellationToken) =>
        capabilities.GetAsync(cancellationToken);
}
