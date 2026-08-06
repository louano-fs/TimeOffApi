using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class TimeClockServiceTests
{
    [Fact]
    public async Task ClockIn_stores_utc_and_uses_the_users_local_shift_date()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Service.ClockInAsync(
            "2026-07-30T08:00:00+08:00", CancellationToken.None);

        result.Start.Should().Be(new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc));
        result.ShiftDate.Should().Be(new DateOnly(2026, 7, 30));
        result.Status.Should().Be("Working");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task ClockIn_twice_returns_a_domain_conflict()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);

        var action = () => fixture.Service.ClockInAsync(
            "2026-07-30T08:01:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("ACTIVE_OR_OVERLAPPING_WORK_SESSION");
    }

    [Fact]
    public async Task Concurrent_clock_ins_create_only_one_active_session()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var attempts = Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await fixture.Service.ClockInAsync(
                    "2026-07-30T08:00:00+08:00", CancellationToken.None);
                return "created";
            }
            catch (ConflictException)
            {
                return "conflict";
            }
        });

        var results = await Task.WhenAll(attempts);

        results.Should().ContainSingle(x => x == "created");
        results.Should().ContainSingle(x => x == "conflict");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Completed_session_subtracts_break_time()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        await fixture.Service.StartBreakAsync("2026-07-30T12:00:00+08:00", CancellationToken.None);
        await fixture.Service.EndBreakAsync("2026-07-30T13:00:00+08:00", CancellationToken.None);

        var result = await fixture.Service.ClockOutAsync(
            "2026-07-30T17:00:00+08:00", CancellationToken.None);

        result.Status.Should().Be("Completed");
        result.WorkedMinutes.Should().Be(480);
    }

    [Fact]
    public async Task ClockOut_while_on_break_is_rejected()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        await fixture.Service.StartBreakAsync("2026-07-30T12:00:00+08:00", CancellationToken.None);

        var action = () => fixture.Service.ClockOutAsync(
            "2026-07-30T17:00:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("ACTIVE_BREAK_EXISTS");
    }

    [Fact]
    public async Task ClockOut_before_a_completed_break_end_is_rejected()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        await fixture.Service.StartBreakAsync("2026-07-30T12:00:00+08:00", CancellationToken.None);
        await fixture.Service.EndBreakAsync("2026-07-30T13:00:00+08:00", CancellationToken.None);

        var action = () => fixture.Service.ClockOutAsync(
            "2026-07-30T12:30:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("CLOCK_OUT_BEFORE_BREAK_END");
        (await fixture.Db.TimeLogs.SingleAsync(
            x => x.Type == TimeLogType.Work,
            TestContext.Current.CancellationToken)).End.Should().BeNull();
    }

    [Fact]
    public async Task ClockOut_without_an_active_work_session_returns_a_domain_conflict()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.Service.ClockOutAsync(
            "2026-07-30T17:00:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("NO_ACTIVE_WORK_SESSION");
    }

    [Theory]
    [InlineData("2026-07-30T08:00:00+08:00")]
    [InlineData("2026-07-30T07:59:59+08:00")]
    public async Task ClockOut_must_be_later_than_clock_in(string clockOut)
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);

        var action = () => fixture.Service.ClockOutAsync(clockOut, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("INVALID_CLOCK_OUT");
        (await fixture.Db.TimeLogs.SingleAsync(TestContext.Current.CancellationToken))
            .End.Should().BeNull();
    }

    [Fact]
    public async Task ClockOut_preserves_seconds_and_reports_only_complete_worked_minutes()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);

        var result = await fixture.Service.ClockOutAsync(
            "2026-07-30T08:01:59+08:00", CancellationToken.None);

        result.End.Should().Be(new DateTime(2026, 7, 30, 0, 1, 59, DateTimeKind.Utc));
        result.WorkedMinutes.Should().Be(1);
    }

    [Fact]
    public async Task ClockOut_does_not_cap_an_overtime_length_session()
    {
        await using var fixture = await TestFixture.CreateAsync(
            now: new DateTimeOffset(2026, 7, 30, 13, 0, 0, TimeSpan.Zero));
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);

        var result = await fixture.Service.ClockOutAsync(
            "2026-07-30T18:30:45+08:00", CancellationToken.None);

        result.WorkedMinutes.Should().Be(630);
    }

    [Fact]
    public async Task Clock_actions_do_not_require_a_manager_relationship()
    {
        await using var fixture = await TestFixture.CreateAsync();

        // An employee can continue clocking time before a manager is assigned.
        var clockIn = await fixture.Service.ClockInAsync(
            "2026-07-30T08:00:00+08:00", CancellationToken.None);
        var clockOut = await fixture.Service.ClockOutAsync(
            "2026-07-30T09:00:00+08:00", CancellationToken.None);

        clockIn.Status.Should().Be("Working");
        clockOut.Status.Should().Be("Completed");
        clockOut.WorkedMinutes.Should().Be(60);
    }

    [Fact]
    public async Task ClockIn_during_a_completed_session_is_rejected_as_an_overlap()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        await fixture.Service.ClockOutAsync("2026-07-30T09:00:00+08:00", CancellationToken.None);

        var action = () => fixture.Service.ClockInAsync(
            "2026-07-30T08:30:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("ACTIVE_OR_OVERLAPPING_WORK_SESSION");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task ClockIn_at_the_previous_clock_out_time_starts_an_adjacent_session()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        await fixture.Service.ClockOutAsync("2026-07-30T09:00:00+08:00", CancellationToken.None);

        var result = await fixture.Service.ClockInAsync(
            "2026-07-30T09:00:00+08:00", CancellationToken.None);

        result.Status.Should().Be("Working");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task ClockIn_ignores_a_deleted_work_session()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.TimeLogs.Add(new TimeLog
        {
            UserId = fixture.User.Id,
            ShiftDate = new DateTime(2026, 7, 30),
            Start = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Type = TimeLogType.Work,
            Timezone = fixture.User.Timezone,
            IsDeleted = true,
            CreatedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
        });
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await fixture.Service.ClockInAsync(
            "2026-07-30T08:30:00+08:00", CancellationToken.None);

        result.Status.Should().Be("Working");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task Inactive_employee_cannot_clock_in()
    {
        await using var fixture = await TestFixture.CreateAsync(isActive: false);

        var action = () => fixture.Service.ClockInAsync(
            "2026-07-30T08:00:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("USER_INACTIVE");
        (await fixture.Db.TimeLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Employee_deactivated_after_clock_in_cannot_clock_out()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync("2026-07-30T08:00:00+08:00", CancellationToken.None);
        fixture.User.IsActive = false;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => fixture.Service.ClockOutAsync(
            "2026-07-30T09:00:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("USER_INACTIVE");
        (await fixture.Db.TimeLogs.SingleAsync(TestContext.Current.CancellationToken))
            .End.Should().BeNull();
    }

    [Fact]
    public async Task ClockOut_ignores_a_deleted_active_work_session()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.TimeLogs.Add(new TimeLog
        {
            UserId = fixture.User.Id,
            ShiftDate = new DateTime(2026, 7, 30),
            Start = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Type = TimeLogType.Work,
            Timezone = fixture.User.Timezone,
            IsDeleted = true,
            CreatedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
        });
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => fixture.Service.ClockOutAsync(
            "2026-07-30T09:00:00+08:00", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("NO_ACTIVE_WORK_SESSION");
        (await fixture.Db.TimeLogs.SingleAsync(TestContext.Current.CancellationToken))
            .End.Should().BeNull();
    }

    [Fact]
    public async Task Status_reports_an_active_session_and_live_worked_minutes()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync(
            "2026-07-30T16:00:00+08:00", CancellationToken.None);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.Status.Should().Be("Working");
        status.ActiveWorkLogId.Should().NotBeNull();
        status.WorkedMinutesToday.Should().Be(120);
        status.WorkedSecondsToday.Should().Be(7_200);
        status.BreakSecondsToday.Should().Be(0);
    }

    [Fact]
    public async Task Status_reports_exact_live_seconds_during_a_break()
    {
        await using var fixture = await TestFixture.CreateAsync(
            now: new DateTimeOffset(2026, 7, 30, 10, 0, 30, TimeSpan.Zero));
        await fixture.Service.ClockInAsync(
            "2026-07-30T16:00:00+08:00", CancellationToken.None);
        await fixture.Service.StartBreakAsync(
            "2026-07-30T17:00:10+08:00", CancellationToken.None);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.Status.Should().Be("OnBreak");
        status.WorkedSecondsToday.Should().Be(3_610);
        status.BreakSecondsToday.Should().Be(3_620);
    }

    [Fact]
    public async Task Status_clips_an_active_work_session_to_the_current_local_day()
    {
        await using var fixture = await TestFixture.CreateAsync(
            now: new DateTimeOffset(2026, 7, 30, 16, 0, 30, TimeSpan.Zero));
        await fixture.Service.ClockInAsync(
            "2026-07-30T23:59:50+08:00", CancellationToken.None);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.Status.Should().Be("Working");
        status.WorkedSecondsToday.Should().Be(30);
        status.BreakSecondsToday.Should().Be(0);
    }

    [Fact]
    public async Task Status_clips_an_active_break_to_the_current_local_day()
    {
        await using var fixture = await TestFixture.CreateAsync(
            now: new DateTimeOffset(2026, 7, 30, 16, 0, 30, TimeSpan.Zero));
        await fixture.Service.ClockInAsync(
            "2026-07-30T23:59:40+08:00", CancellationToken.None);
        await fixture.Service.StartBreakAsync(
            "2026-07-30T23:59:55+08:00", CancellationToken.None);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.Status.Should().Be("OnBreak");
        status.WorkedSecondsToday.Should().Be(0);
        status.BreakSecondsToday.Should().Be(30);
    }

    [Fact]
    public async Task Status_floors_seconds_after_aggregating_all_sessions()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Db.TimeLogs.AddRange(
            CompletedWork(fixture.User.Id, 8, 0, 0, 100, 8, 0, 1, 700),
            CompletedWork(fixture.User.Id, 9, 0, 0, 100, 9, 0, 1, 700));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.WorkedSecondsToday.Should().Be(3);
    }

    [Fact]
    public void Timestamp_without_an_offset_is_rejected()
    {
        var action = () => DateTimeHelper.ParseUtc(
            "2026-07-30T08:00:00",
            new DateTimeOffset(2026, 7, 30, 10, 0, 0, TimeSpan.Zero));

        action.Should().Throw<ValidationException>()
            .Which.Code.Should().Be("OFFSET_REQUIRED");
    }

    private static TimeLog CompletedWork(
        int userId,
        int startHour,
        int startMinute,
        int startSecond,
        int startMillisecond,
        int endHour,
        int endMinute,
        int endSecond,
        int endMillisecond) =>
        new()
        {
            UserId = userId,
            ShiftDate = new DateTime(2026, 7, 30),
            Start = new DateTime(
                2026, 7, 30, startHour, startMinute, startSecond, startMillisecond, DateTimeKind.Utc),
            End = new DateTime(
                2026, 7, 30, endHour, endMinute, endSecond, endMillisecond, DateTimeKind.Utc),
            Type = TimeLogType.Work,
            Timezone = "Asia/Manila",
            CreatedAt = new DateTime(2026, 7, 30, startHour, startMinute, startSecond, DateTimeKind.Utc)
        };

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            User user,
            TimeClockService service)
        {
            _connection = connection;
            Db = db;
            User = user;
            Service = service;
        }

        public AppDbContext Db { get; }
        public User User { get; }
        public TimeClockService Service { get; }

        public static async Task<TestFixture> CreateAsync(
            bool isActive = true,
            DateTimeOffset? now = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var user = new User
            {
                EmployeeId = 1001,
                EmployeeNumber = "EMP-1001",
                Email = "employee@example.com",
                PasswordHash = "not-used",
                FirstName = "Test",
                LastName = "Employee",
                Timezone = "Asia/Manila",
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(user.Id);
            var timeProvider = new FixedTimeProvider(now
                ?? new DateTimeOffset(2026, 7, 30, 10, 0, 0, TimeSpan.Zero));
            var service = new TimeClockService(
                db, currentUser.Object, new UserLockService(), timeProvider);
            return new TestFixture(connection, db, user, service);
        }

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
