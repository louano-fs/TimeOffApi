using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class TimeReportingServiceTests
{
    [Fact]
    public async Task Personal_report_clips_cross_midnight_and_active_sessions_to_the_local_day()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 15, 30),
            Utc(2026, 8, 5, 17, 30),
            (Utc(2026, 8, 5, 16, 15), Utc(2026, 8, 5, 16, 45)));
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 6, 0, 0),
            null,
            (Utc(2026, 8, 6, 3, 0), null));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.ServiceFor(fixture.Employee.Id).GetPersonalAsync(
            new TimeReportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        result.ReportingTimezone.Should().Be("Asia/Manila");
        result.AsOf.Should().Be(Utc(2026, 8, 6, 4, 0));
        result.WorkedSeconds.Should().Be(14_400);
        result.BreakSeconds.Should().Be(5_400);
        result.WorkSessionCount.Should().Be(2);
        result.Daily.Should().ContainSingle().Which.Should().Be(
            new DailyTimeReportResponse(new DateOnly(2026, 8, 6), 14_400, 5_400));
    }

    [Fact]
    public async Task Personal_report_for_a_future_range_returns_zero_without_future_work()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 6, 0, 0),
            null);
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.ServiceFor(fixture.Employee.Id).GetPersonalAsync(
            new TimeReportQuery
            {
                StartDate = new DateOnly(2026, 8, 7),
                EndDate = new DateOnly(2026, 8, 7)
            },
            TestContext.Current.CancellationToken);

        result.WorkedSeconds.Should().Be(0);
        result.BreakSeconds.Should().Be(0);
        result.WorkSessionCount.Should().Be(0);
        result.Daily.Should().ContainSingle().Which.WorkedSeconds.Should().Be(0);
    }

    [Fact]
    public async Task Team_report_uses_current_active_direct_reports_and_includes_zero_hour_members()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 19, 0),
            Utc(2026, 8, 6, 4, 0),
            (Utc(2026, 8, 5, 23, 0), Utc(2026, 8, 6, 0, 0)));
        fixture.AddWork(
            fixture.InactiveEmployee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 6, 0, 0));
        fixture.AddWork(
            fixture.OtherEmployee.Id,
            Utc(2026, 8, 5, 18, 0),
            Utc(2026, 8, 6, 4, 0));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.ServiceFor(fixture.Manager.Id).GetTeamAsync(
            new TeamTimeReportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        result.ReportingTimezone.Should().Be("Asia/Manila");
        result.IncludedMemberCount.Should().Be(2);
        result.ExcludedInactiveCount.Should().Be(1);
        result.TotalWorkedSeconds.Should().Be(28_800);
        result.TotalBreakSeconds.Should().Be(3_600);
        result.AverageWorkedSeconds.Should().Be(14_400);
        result.Members.Should().HaveCount(2);
        result.Members.Should().Contain(x =>
            x.UserId == fixture.Employee.Id
            && x.WorkedSeconds == 28_800
            && x.BreakSeconds == 3_600
            && x.WorkSessionCount == 1);
        result.Members.Should().Contain(x =>
            x.UserId == fixture.ZeroHourEmployee.Id
            && x.WorkedSeconds == 0
            && x.BreakSeconds == 0
            && x.WorkSessionCount == 0);
        result.Members.Should().NotContain(x => x.UserId == fixture.InactiveEmployee.Id);
        result.Members.Should().NotContain(x => x.UserId == fixture.OtherEmployee.Id);
    }

    [Fact]
    public async Task Team_report_can_include_current_inactive_direct_reports()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.AddWork(
            fixture.InactiveEmployee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 6, 0, 0));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.ServiceFor(fixture.Manager.Id).GetTeamAsync(
            new TeamTimeReportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6),
                IncludeInactive = true
            },
            TestContext.Current.CancellationToken);

        result.IncludedMemberCount.Should().Be(3);
        result.ExcludedInactiveCount.Should().Be(0);
        result.Members.Should().Contain(x =>
            x.UserId == fixture.InactiveEmployee.Id
            && !x.IsActive
            && x.WorkedSeconds == 14_400);
    }

    [Fact]
    public async Task Team_report_rejects_a_caller_whose_current_role_is_not_manager()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.ServiceFor(fixture.Employee.Id).GetTeamAsync(
            new TeamTimeReportQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("MANAGER_ACCESS_REQUIRED");
    }

    [Fact]
    public async Task Team_report_rejects_ranges_longer_than_ninety_two_days()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.ServiceFor(fixture.Manager.Id).GetTeamAsync(
            new TeamTimeReportQuery
            {
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 4, 3)
            },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("REPORT_RANGE_TOO_LARGE");
    }

    [Fact]
    public async Task Team_report_rejects_more_than_two_hundred_included_members()
    {
        await using var fixture = await TestFixture.CreateAsync();
        for (var index = 0; index < 199; index++)
        {
            fixture.Db.Users.Add(TestFixture.User(
                employeeId: 2_000 + index,
                employeeNumber: $"EMP-{2_000 + index}",
                role: UserRole.Employee,
                manager: fixture.Manager));
        }
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => fixture.ServiceFor(fixture.Manager.Id).GetTeamAsync(
            new TeamTimeReportQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("TEAM_REPORT_TOO_LARGE");
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FixedTimeProvider _timeProvider = new(
            new DateTimeOffset(2026, 8, 6, 4, 0, 0, TimeSpan.Zero));

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            User manager,
            User otherManager,
            User employee,
            User zeroHourEmployee,
            User inactiveEmployee,
            User otherEmployee)
        {
            _connection = connection;
            Db = db;
            Manager = manager;
            OtherManager = otherManager;
            Employee = employee;
            ZeroHourEmployee = zeroHourEmployee;
            InactiveEmployee = inactiveEmployee;
            OtherEmployee = otherEmployee;
        }

        public AppDbContext Db { get; }
        public User Manager { get; }
        public User OtherManager { get; }
        public User Employee { get; }
        public User ZeroHourEmployee { get; }
        public User InactiveEmployee { get; }
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

            var manager = User(8_000, "MGR-8000", UserRole.Manager);
            var otherManager = User(8_001, "MGR-8001", UserRole.Manager);
            var employee = User(1_001, "EMP-1001", UserRole.Employee, manager);
            employee.FirstName = "Ada";
            employee.LastName = "Active";
            var zeroHourEmployee = User(1_002, "EMP-1002", UserRole.Employee, manager);
            zeroHourEmployee.FirstName = "Zoe";
            zeroHourEmployee.LastName = "Zero";
            var inactiveEmployee = User(1_003, "EMP-1003", UserRole.Employee, manager);
            inactiveEmployee.IsActive = false;
            var otherEmployee = User(1_004, "EMP-1004", UserRole.Employee, otherManager);
            db.Users.AddRange(
                manager,
                otherManager,
                employee,
                zeroHourEmployee,
                inactiveEmployee,
                otherEmployee);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return new(
                connection,
                db,
                manager,
                otherManager,
                employee,
                zeroHourEmployee,
                inactiveEmployee,
                otherEmployee);
        }

        public TimeReportingService ServiceFor(int userId)
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(userId);
            return new(
                Db,
                currentUser.Object,
                _timeProvider,
                new TeamTimeReportingService(Db));
        }

        public void AddWork(
            int userId,
            DateTime start,
            DateTime? end,
            params (DateTime Start, DateTime? End)[] breaks)
        {
            var work = new TimeLog
            {
                UserId = userId,
                ShiftDate = DateTimeHelper.LocalDate(start, "Asia/Manila"),
                Start = start,
                End = end,
                Type = TimeLogType.Work,
                Timezone = "Asia/Manila",
                CreatedAt = start,
                Breaks = breaks.Select(item => new TimeLog
                {
                    UserId = userId,
                    ShiftDate = DateTimeHelper.LocalDate(start, "Asia/Manila"),
                    Start = item.Start,
                    End = item.End,
                    Type = TimeLogType.Break,
                    Timezone = "Asia/Manila",
                    CreatedAt = item.Start
                }).ToArray()
            };
            Db.TimeLogs.Add(work);
        }

        public static User User(
            int employeeId,
            string employeeNumber,
            UserRole role,
            User? manager = null) =>
            new()
            {
                EmployeeId = employeeId,
                EmployeeNumber = employeeNumber,
                Email = $"{employeeNumber.ToLowerInvariant()}@example.com",
                PasswordHash = "not-used",
                FirstName = employeeNumber,
                LastName = "User",
                Role = role,
                Manager = manager,
                Timezone = "Asia/Manila",
                IsActive = true,
                CreatedAt = Utc(2026, 8, 1, 0, 0)
            };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
