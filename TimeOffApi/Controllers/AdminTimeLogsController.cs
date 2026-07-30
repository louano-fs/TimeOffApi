using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeOffApi.Contracts;
using TimeOffApi.Domain;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class AdminTimeLogsController(
    ITimeLogService timeLogs,
    ITimeClockService timeClock) : ControllerBase
{
    [HttpGet("time-logs")]
    public async Task<PagedResponse<WorkSessionResponse>> Get(
        [FromQuery] AdminTimeLogQuery query,
        [FromServices] IValidator<AdminTimeLogQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await timeLogs.GetAdminAsync(query, cancellationToken);
    }

    [HttpGet("users/{userId:int}/time-clock/status")]
    public Task<ClockStatusResponse> Status(int userId, CancellationToken cancellationToken) =>
        timeClock.GetStatusAsync(userId, cancellationToken);
}
