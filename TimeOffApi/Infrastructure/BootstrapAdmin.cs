using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Domain;

namespace TimeOffApi.Infrastructure;

public static class BootstrapAdmin
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
            return;

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "BootstrapAdmin email and password must be supplied when bootstrap is enabled.");

        var db = services.GetRequiredService<AppDbContext>();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail))
            return;

        var employeeId = configuration.GetValue("BootstrapAdmin:EmployeeId", 1);
        var employeeNumber = configuration["BootstrapAdmin:EmployeeNumber"] ?? "ADMIN-001";
        var user = new User
        {
            EmployeeId = employeeId,
            EmployeeNumber = employeeNumber,
            Email = normalizedEmail,
            FirstName = configuration["BootstrapAdmin:FirstName"] ?? "System",
            LastName = configuration["BootstrapAdmin:LastName"] ?? "Administrator",
            Role = UserRole.Administrator,
            Timezone = configuration["BootstrapAdmin:Timezone"] ?? "Asia/Manila",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>()
            .HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
