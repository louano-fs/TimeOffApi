using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeOffApi.Contracts;
using TimeOffApi.Controllers;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class TeamServiceTests
{
    [Fact]
    public void Team_endpoints_require_the_manager_role()
    {
        var authorize = typeof(TeamController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be(nameof(UserRole.Manager));
    }

    [Fact]
    public async Task GetMembers_returns_only_the_authenticated_managers_direct_reports()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var members = await fixture.TeamService.GetMembersAsync(TestContext.Current.CancellationToken);

        members.Should().ContainSingle();
        members.Single().UserId.Should().Be(fixture.DirectReport.Id);
        members.Single().EmployeeNumber.Should().Be("EMP-1001");
    }

    [Fact]
    public async Task GetTeamMember_returns_logs_for_an_assigned_employee()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.TimeLogService.GetTeamMemberAsync(
            fixture.DirectReport.Id,
            new TimeLogQuery(),
            TestContext.Current.CancellationToken);

        result.Items.Should().ContainSingle();
        result.Items.Single().EmployeeId.Should().Be(fixture.DirectReport.EmployeeId);
    }

    [Fact]
    public async Task GetTeamMember_hides_an_employee_assigned_to_another_manager()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.TimeLogService.GetTeamMemberAsync(
            fixture.OtherEmployee.Id,
            new TimeLogQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("TEAM_MEMBER_NOT_FOUND");
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            TeamService teamService,
            TimeLogService timeLogService,
            User directReport,
            User otherEmployee)
        {
            _connection = connection;
            Db = db;
            TeamService = teamService;
            TimeLogService = timeLogService;
            DirectReport = directReport;
            OtherEmployee = otherEmployee;
        }

        public AppDbContext Db { get; }
        public TeamService TeamService { get; }
        public TimeLogService TimeLogService { get; }
        public User DirectReport { get; }
        public User OtherEmployee { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var manager = User(8000, "MGR-8000", UserRole.Manager);
            var otherManager = User(8001, "MGR-8001", UserRole.Manager);
            var directReport = User(1001, "EMP-1001", UserRole.Employee);
            var otherEmployee = User(1002, "EMP-1002", UserRole.Employee);
            directReport.Manager = manager;
            otherManager.Manager = manager;
            otherEmployee.Manager = otherManager;
            db.Users.AddRange(manager, otherManager, directReport, otherEmployee);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            db.TimeLogs.AddRange(
                WorkLog(directReport.Id),
                WorkLog(otherEmployee.Id));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(manager.Id);
            return new TestFixture(
                connection,
                db,
                new TeamService(db, currentUser.Object),
                new TimeLogService(db, currentUser.Object, TimeProvider.System),
                directReport,
                otherEmployee);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static User User(int employeeId, string employeeNumber, UserRole role) => new()
        {
            EmployeeId = employeeId,
            EmployeeNumber = employeeNumber,
            Email = $"{employeeNumber.ToLowerInvariant()}@example.com",
            PasswordHash = "not-used",
            FirstName = employeeNumber,
            LastName = "User",
            Role = role,
            Timezone = "Asia/Manila",
            IsActive = true,
            CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        private static TimeLog WorkLog(int userId) => new()
        {
            UserId = userId,
            ShiftDate = new DateTime(2026, 8, 5),
            Start = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
            Type = TimeLogType.Work,
            Timezone = "Asia/Manila",
            CreatedAt = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
