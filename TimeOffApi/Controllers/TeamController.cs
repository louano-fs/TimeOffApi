using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeOffApi.Contracts;
using TimeOffApi.Domain;
using TimeOffApi.Services;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/team")]
[Authorize(Roles = nameof(UserRole.Manager))]
public sealed class TeamController(
    ITeamService team,
    ITimeLogService timeLogs,
    ITimeReportingService reporting,
    ITimeLogExportService exports) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<TeamMemberResponse>> Get(
        CancellationToken cancellationToken) =>
        team.GetMembersAsync(cancellationToken);

    [HttpGet("{userId:int}/time-logs")]
    public async Task<PagedResponse<WorkSessionResponse>> GetTimeLogs(
        int userId,
        [FromQuery] TimeLogQuery query,
        [FromServices] IValidator<TimeLogQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await timeLogs.GetTeamMemberAsync(userId, query, cancellationToken);
    }

    [HttpGet("report")]
    public async Task<TeamTimeReportResponse> Report(
        [FromQuery] TeamTimeReportQuery query,
        [FromServices] IValidator<TeamTimeReportQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        return await reporting.GetTeamAsync(query, cancellationToken);
    }

    [HttpGet("time-logs/export")]
    public async Task<FileContentResult> Export(
        [FromQuery] TeamTimeLogExportQuery query,
        [FromServices] IValidator<TeamTimeLogExportQuery> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        var file = await exports.ExportTeamAsync(query, cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }
}
