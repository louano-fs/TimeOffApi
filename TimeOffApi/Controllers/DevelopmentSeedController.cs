using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Domain;

namespace TimeOffApi.Controllers;

[ApiController]
[Route("api/development/seed-preview")]
[AllowAnonymous]
public sealed class DevelopmentSeedController(
    AppDbContext db,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Get(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var employees = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Employee)
            .OrderBy(x => x.EmployeeId)
            .Select(x => new
            {
                x.Id,
                x.EmployeeId,
                x.EmployeeNumber,
                Name = x.FirstName + " " + x.LastName,
                x.Email,
                Role = x.Role.ToString(),
                x.Timezone,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var requests = await db.TimeOffRequests.AsNoTracking()
            .Include(x => x.User)
            .OrderBy(x => x.StartDate)
            .Select(x => new
            {
                x.Id,
                x.User.EmployeeId,
                x.User.EmployeeNumber,
                Employee = x.User.FirstName + " " + x.User.LastName,
                Type = x.Type.ToString(),
                StartDate = DateOnly.FromDateTime(x.StartDate),
                EndDate = DateOnly.FromDateTime(x.EndDate),
                Status = x.Status.ToString(),
                x.Reason
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            employeeCount = employees.Count,
            pendingRequestCount = requests.Count(x => x.Status == "Pending"),
            developmentLogin = new
            {
                administratorEmail = "admin@timeclock.local",
                employeeEmail = "employee01@timeclock.local",
                password = "Employee!2026"
            },
            employees,
            pendingTimeOffRequests = requests
        });
    }
}
