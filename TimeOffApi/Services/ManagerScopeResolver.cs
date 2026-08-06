using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public sealed record ManagerScope(
    int ManagerId,
    string Timezone,
    DateTime AsOf);

public interface IManagerScopeResolver
{
    Task<ManagerScope?> TryResolveAsync(CancellationToken cancellationToken);
    Task<ManagerScope> ResolveRequiredAsync(CancellationToken cancellationToken);
}

public sealed class ManagerScopeResolver(
    AppDbContext db,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IManagerScopeResolver
{
    public async Task<ManagerScope?> TryResolveAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(UserRole.Manager))
            return null;

        var manager = await db.Users.AsNoTracking()
            .Where(x => x.Id == currentUser.UserId)
            .Select(x => new { x.Id, x.Role, x.IsActive, x.Timezone })
            .SingleOrDefaultAsync(cancellationToken);

        if (manager is null || manager.Role != UserRole.Manager || !manager.IsActive)
            return null;

        return new(
            manager.Id,
            manager.Timezone,
            timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<ManagerScope> ResolveRequiredAsync(CancellationToken cancellationToken) =>
        await TryResolveAsync(cancellationToken)
        ?? throw new ForbiddenException(
            "MANAGER_ACCESS_REQUIRED",
            "Current active manager access is required.");
}
