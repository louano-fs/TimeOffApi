using System.Security.Claims;

namespace TimeOffApi.Infrastructure;

public interface ICurrentUserService
{
    int UserId { get; }
}

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public int UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedException("INVALID_TOKEN", "The access token is invalid.");
        }
    }
}
