using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeOffApi.Contracts;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/time-clock")]
[Authorize]
public sealed class TimeClockController(ITimeClockService service) : ControllerBase
{
    [HttpPost("clock-in")]
    public Task<TimeLogResponse> ClockIn(
        ClockActionRequest request,
        [FromServices] IValidator<ClockActionRequest> validator,
        CancellationToken cancellationToken) =>
        ValidateAndRun(request, validator, service.ClockInAsync, cancellationToken);

    [HttpPost("break/start")]
    public Task<TimeLogResponse> StartBreak(
        ClockActionRequest request,
        [FromServices] IValidator<ClockActionRequest> validator,
        CancellationToken cancellationToken) =>
        ValidateAndRun(request, validator, service.StartBreakAsync, cancellationToken);

    [HttpPost("break/end")]
    public Task<TimeLogResponse> EndBreak(
        ClockActionRequest request,
        [FromServices] IValidator<ClockActionRequest> validator,
        CancellationToken cancellationToken) =>
        ValidateAndRun(request, validator, service.EndBreakAsync, cancellationToken);

    [HttpPost("clock-out")]
    public Task<TimeLogResponse> ClockOut(
        ClockActionRequest request,
        [FromServices] IValidator<ClockActionRequest> validator,
        CancellationToken cancellationToken) =>
        ValidateAndRun(request, validator, service.ClockOutAsync, cancellationToken);

    [HttpGet("status")]
    public Task<ClockStatusResponse> Status(CancellationToken cancellationToken) =>
        service.GetStatusAsync(null, cancellationToken);

    private static async Task<TimeLogResponse> ValidateAndRun(
        ClockActionRequest request,
        IValidator<ClockActionRequest> validator,
        Func<string?, CancellationToken, Task<TimeLogResponse>> action,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        return await action(request.DateTime, cancellationToken);
    }
}
