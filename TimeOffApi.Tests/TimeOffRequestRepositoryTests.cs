using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class TimeOffRequestRepositoryTests
{
    [Fact]
    public async Task Competing_decisions_only_allow_the_first_pending_transition()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var firstDb = new AppDbContext(options);
        await firstDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var request = new TimeOffRequest
        {
            User = new User
            {
                EmployeeId = 1001,
                EmployeeNumber = "EMP-1001",
                Email = "employee01@example.com",
                PasswordHash = "not-used",
                FirstName = "Test",
                LastName = "Employee",
                Timezone = "Asia/Manila",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            StartDate = new DateTime(2026, 8, 3),
            EndDate = new DateTime(2026, 8, 4),
            Type = TimeOffType.Vacation,
            Status = TimeOffRequestStatus.Pending,
            Reason = "Family trip",
            CreatedAt = DateTime.UtcNow
        };
        firstDb.TimeOffRequests.Add(request);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var secondDb = new AppDbContext(options);
        var firstRepository = new TimeOffRequestRepository(firstDb);
        var secondRepository = new TimeOffRequestRepository(secondDb);

        var approved = await firstRepository.TryChangeStatusAsync(
            request.Id,
            TimeOffRequestStatus.Pending,
            TimeOffRequestStatus.Approved,
            TestContext.Current.CancellationToken);
        var rejected = await secondRepository.TryChangeStatusAsync(
            request.Id,
            TimeOffRequestStatus.Pending,
            TimeOffRequestStatus.Rejected,
            TestContext.Current.CancellationToken);

        approved.Should().BeTrue();
        rejected.Should().BeFalse();
        (await secondDb.TimeOffRequests.AsNoTracking()
                .SingleAsync(
                    x => x.Id == request.Id,
                    TestContext.Current.CancellationToken))
            .Status.Should().Be(TimeOffRequestStatus.Approved);
    }
}
