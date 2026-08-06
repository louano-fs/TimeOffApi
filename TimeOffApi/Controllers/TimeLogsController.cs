using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeOffApi.Contracts;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/time-logs")]
[Authorize]
public sealed class TimeLogsController(
    ITimeLogService service,
    ITimeReportingService reporting) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResponse<WorkSessionResponse>> Get(
        [FromQuery] TimeLogQuery query,
        [FromServices] IValidator<TimeLogQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await service.GetMineAsync(query, cancellationToken);
    }

    [HttpGet("{id:int}")]
    public Task<WorkSessionResponse> GetById(int id, CancellationToken cancellationToken) =>
        service.GetMineByIdAsync(id, cancellationToken);

    [HttpGet("summary")]
    public async Task<TimeSummaryResponse> Summary(
        [FromQuery] SummaryQuery query,
        [FromServices] IValidator<SummaryQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await service.GetSummaryAsync(query, cancellationToken);
    }

    [HttpGet("report")]
    public async Task<PersonalTimeReportResponse> Report(
        [FromQuery] TimeReportQuery query,
        [FromServices] IValidator<TimeReportQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await reporting.GetPersonalAsync(query, cancellationToken);
    }
}
