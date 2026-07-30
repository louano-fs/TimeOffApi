using FluentValidation;

namespace TimeOffApi.Contracts;

public sealed record RegisterRequest(
    int EmployeeId,
    string EmployeeNumber,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Timezone = "Asia/Manila");

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    int UserId,
    int EmployeeId,
    string EmployeeNumber,
    string Email,
    string FirstName,
    string LastName,
    string Role);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100)
            .Must(BeValidTimeZone).WithMessage("Timezone must be a valid IANA or system timezone.");
    }

    private static bool BeValidTimeZone(string id)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { return false; }
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
