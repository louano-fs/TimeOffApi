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
    ITimeReportingService reporting) : ControllerBase
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
}
