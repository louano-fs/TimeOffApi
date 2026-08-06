using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class ManagerAssistantTeamToolServiceTests
{
    private static readonly DateOnly Today = new(2026, 8, 6);

    [Fact]
    public async Task Team_totals_include_zero_time_members_and_apply_stable_ranks_before_limit()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Tools.GetTeamWorkedTimeAsync(
            fixture.Scope,
            new(Today, Today, Order: TeamWorkedTimeOrder.WorkedDescending, Limit: 2),
            TestContext.Current.CancellationToken);

        result.IncludedMemberCount.Should().Be(5);
        result.ExcludedInactiveCount.Should().Be(1);
        result.TotalWorkedSeconds.Should().Be(45_000);
        result.Members.Select(x => x.EmployeeNumber).Should().Equal("EMP-1001", "EMP-1002");
        result.Members.Select(x => x.Rank).Should().Equal(1, 2);
        result.Members.Single(x => x.EmployeeNumber == "EMP-1002").ClockStatus
            .Should().Be(TeamClockStatus.OnBreak);
        result.PeriodComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Threshold_uses_exact_seconds_excludes_the_boundary_and_includes_zero_time_members()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Tools.FindTeamMembersByWorkedTimeAsync(
            fixture.Scope,
            new(
                Today,
                Today,
                WorkedTimeComparison.LessThan,
                8,
                WorkedTimeUnit.Hours),
            TestContext.Current.CancellationToken);

        result.ThresholdSeconds.Should().Be(28_800);
        result.MatchingMemberCount.Should().Be(4);
        result.Members.Should().NotContain(x => x.EmployeeNumber == "EMP-1001");
        result.Members.Should().Contain(x => x.EmployeeNumber == "EMP-1003"
            && x.WorkedSeconds == 0);
    }

    [Fact]
    public async Task Current_status_is_a_snapshot_not_an_attendance_judgment()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Tools.GetTeamCurrentStatusAsync(
            fixture.Scope,
            new(),
            TestContext.Current.CancellationToken);

        result.Members.Single(x => x.EmployeeNumber == "EMP-1001").ClockStatus
            .Should().Be(TeamClockStatus.ClockedOut);
        result.Members.Single(x => x.EmployeeNumber == "EMP-1002").ClockStatus
            .Should().Be(TeamClockStatus.OnBreak);
        result.Members.Single(x => x.EmployeeNumber == "EMP-1005").ClockStatus
            .Should().Be(TeamClockStatus.Working);
    }

    [Fact]
    public async Task Named_member_returns_evidence_or_current_team_clarification()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var exact = await fixture.Tools.GetDirectReportWorkedTimeAsync(
            fixture.Scope,
            new("EMP-1001", Today, Today),
            TestContext.Current.CancellationToken);
        var ambiguous = await fixture.Tools.GetDirectReportWorkedTimeAsync(
            fixture.Scope,
            new("Sam", Today, Today),
            TestContext.Current.CancellationToken);

        exact.Should().BeOfType<DirectReportWorkedTimeToolResult>()
            .Which.Member.WorkedSeconds.Should().Be(28_800);
        ambiguous.Should().BeOfType<TeamMemberClarificationToolResult>()
            .Which.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public async Task Export_preparation_returns_only_a_reauthorized_download_descriptor()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Tools.PrepareTeamTimeLogExportAsync(
            fixture.Scope,
            new(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 21)),
            TestContext.Current.CancellationToken);

        result.FileName.Should().Be("team-time-logs-2026-08-05-to-2026-08-21.xlsx");
        result.DownloadUrl.Should().Be(
            "/api/team/time-logs/export?startDate=2026-08-05&endDate=2026-08-21"
            + "&includeInactive=false&format=xlsx");
        result.IncludedMemberCount.Should().Be(5);
        result.ExcludedInactiveCount.Should().Be(1);
    }

    [Fact]
    public async Task Invalid_limits_and_subsecond_thresholds_fail_instead_of_truncating()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var invalidLimit = () => fixture.Tools.GetTeamWorkedTimeAsync(
            fixture.Scope,
            new(Today, Today, Limit: 6),
            TestContext.Current.CancellationToken);
        var invalidThreshold = () => fixture.Tools.FindTeamMembersByWorkedTimeAsync(
            fixture.Scope,
            new(
                Today,
                Today,
                WorkedTimeComparison.GreaterThan,
                0.1m,
                WorkedTimeUnit.Seconds),
            TestContext.Current.CancellationToken);

        (await invalidLimit.Should().ThrowAsync<ValidationException>())
            .Which.Code.Should().Be("INVALID_LIMIT");
        (await invalidThreshold.Should().ThrowAsync<ValidationException>())
            .Which.Code.Should().Be("INVALID_THRESHOLD");
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            ManagerScope scope,
            ManagerAssistantTeamToolService tools)
        {
            _connection = connection;
            Db = db;
            Scope = scope;
            Tools = tools;
        }

        public AppDbContext Db { get; }
        public ManagerScope Scope { get; }
        public ManagerAssistantTeamToolService Tools { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var manager = User(8_000, "MGR-8000", "Manny", "Manager", UserRole.Manager);
            var ada = User(1_001, "EMP-1001", "Ada", "Active", UserRole.Employee, manager);
            var ben = User(1_002, "EMP-1002", "Ben", "Break", UserRole.Employee, manager);
            var samFirst = User(1_003, "EMP-1003", "Sam", "First", UserRole.Employee, manager);
            var samSecond = User(1_004, "EMP-1004", "Sam", "Second", UserRole.Employee, manager);
            var willa = User(1_005, "EMP-1005", "Willa", "Working", UserRole.Employee, manager);
            var inactive = User(
                1_006, "EMP-1006", "Ina", "Inactive", UserRole.Employee, manager, false);
            db.Users.AddRange(manager, ada, ben, samFirst, samSecond, willa, inactive);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            db.TimeLogs.Add(Work(
                ada.Id,
                Utc(2026, 8, 5, 20, 0),
                Utc(2026, 8, 6, 4, 0)));
            db.TimeLogs.Add(Work(
                ben.Id,
                Utc(2026, 8, 6, 0, 0),
                null,
                (Utc(2026, 8, 6, 3, 30), null)));
            db.TimeLogs.Add(Work(
                willa.Id,
                Utc(2026, 8, 6, 3, 0),
                null));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var scope = new ManagerScope(
                manager.Id,
                "Asia/Manila",
                Utc(2026, 8, 6, 4, 0));
            var reporting = new TeamTimeReportingService(db);
            var managerOptions = Options.Create(new ManagerAssistantOptions());
            var resolver = new DirectReportResolver(db, managerOptions);
            return new(
                connection,
                db,
                scope,
                new ManagerAssistantTeamToolService(db, reporting, resolver, managerOptions));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private static TimeLog Work(
            int userId,
            DateTime start,
            DateTime? end,
            params (DateTime Start, DateTime? End)[] breaks) =>
            new()
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

        private static User User(
            int employeeId,
            string employeeNumber,
            string firstName,
            string lastName,
            UserRole role,
            User? manager = null,
            bool isActive = true) =>
            new()
            {
                EmployeeId = employeeId,
                EmployeeNumber = employeeNumber,
                Email = $"{employeeNumber.ToLowerInvariant()}@example.com",
                PasswordHash = "not-used",
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                Manager = manager,
                Timezone = "Asia/Manila",
                IsActive = isActive,
                CreatedAt = Utc(2026, 8, 1, 0, 0)
            };
    }
}
