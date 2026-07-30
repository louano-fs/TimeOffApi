using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 480;
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email
            || x.EmployeeId == request.EmployeeId
            || x.EmployeeNumber == request.EmployeeNumber.Trim(), cancellationToken))
            throw new ConflictException("USER_ALREADY_EXISTS",
                "A user with that email, employee ID, or employee number already exists.");

        var user = new User
        {
            EmployeeId = request.EmployeeId,
            EmployeeNumber = request.EmployeeNumber.Trim(),
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Timezone = request.Timezone.Trim(),
            Role = UserRole.Employee,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("USER_ALREADY_EXISTS",
                "A user with that email, employee ID, or employee number already exists.");
        }

        return CreateToken(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null
            || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("INVALID_CREDENTIALS", "Email or password is incorrect.");
        if (!user.IsActive)
            throw new ForbiddenException("USER_INACTIVE", "This account is inactive.");

        return CreateToken(user);
    }

    private AuthResponse CreateToken(User user)
    {
        if (Encoding.UTF8.GetByteCount(_jwt.Key) < 32)
            throw new InvalidOperationException("JWT key must be at least 32 bytes.");

        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_jwt.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("employee_id", user.EmployeeId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            now.UtcDateTime,
            expires.UtcDateTime,
            credentials);

        return new(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires.UtcDateTime,
            user.Id,
            user.EmployeeId,
            user.EmployeeNumber,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString());
    }
}
