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
    public async Task Status_reports_an_active_session_and_live_worked_minutes()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.ClockInAsync(
            "2026-07-30T16:00:00+08:00", CancellationToken.None);

        var status = await fixture.Service.GetStatusAsync(null, CancellationToken.None);

        status.Status.Should().Be("Working");
        status.ActiveWorkLogId.Should().NotBeNull();
        status.WorkedMinutesToday.Should().Be(120);
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

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            TimeClockService service)
        {
            _connection = connection;
            Db = db;
            Service = service;
        }

        public AppDbContext Db { get; }
        public TimeClockService Service { get; }

        public static async Task<TestFixture> CreateAsync()
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
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(user.Id);
            var now = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 30, 10, 0, 0, TimeSpan.Zero));
            var service = new TimeClockService(
                db, currentUser.Object, new UserLockService(), now);
            return new TestFixture(connection, db, service);
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
