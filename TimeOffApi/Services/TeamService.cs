using Microsoft.EntityFrameworkCore;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITeamService
{
    Task<IReadOnlyCollection<TeamMemberResponse>> GetMembersAsync(
        CancellationToken cancellationToken);
}

public sealed class TeamService(AppDbContext db, ICurrentUserService currentUser) : ITeamService
{
    public async Task<IReadOnlyCollection<TeamMemberResponse>> GetMembersAsync(
        CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(x => x.ManagerId == currentUser.UserId && x.Role == UserRole.Employee)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new TeamMemberResponse(
                x.Id,
                x.EmployeeId,
                x.EmployeeNumber,
                x.Email,
                x.FirstName,
                x.LastName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
}
