using ClosedXML.Excel;
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

public sealed class TimeLogExportServiceTests
{
    [Fact]
    public async Task Personal_export_writes_clipped_sessions_breaks_and_literal_strings()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Employee.EmployeeNumber = "+EMP-1001";
        fixture.Employee.FirstName = "=SUM(1,1)";
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 15, 30),
            Utc(2026, 8, 5, 17, 30),
            new BreakSpec(Utc(2026, 8, 5, 16, 15), Utc(2026, 8, 5, 16, 45)));
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 6, 0, 0),
            null,
            new BreakSpec(Utc(2026, 8, 6, 3, 0), null));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var file = await fixture.ServiceFor(fixture.Employee.Id).ExportPersonalAsync(
            new TimeLogExportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        file.ContentType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        file.FileName.Should().Be("my-time-logs-2026-08-06-to-2026-08-06.xlsx");
        using var workbook = Open(file);
        workbook.Worksheets.Select(x => x.Name).Should().Equal(
            "Summary", "Work Sessions", "Breaks");

        var summary = workbook.Worksheet("Summary");
        SummaryNumber(summary, "Work Session Count").Should().Be(2);
        SummaryNumber(summary, "Break Count").Should().Be(2);
        SummaryNumber(summary, "Total Elapsed Seconds").Should().Be(19_800);
        SummaryNumber(summary, "Total Break Seconds").Should().Be(5_400);
        SummaryNumber(summary, "Total Worked Seconds").Should().Be(14_400);
        SummaryText(summary, "Reporting Timezone").Should().Be("Asia/Manila");

        var sessions = workbook.Worksheet("Work Sessions");
        sessions.LastRowUsed()!.RowNumber().Should().Be(3);
        sessions.Cell(2, 2).GetString().Should().Be("+EMP-1001");
        sessions.Cell(2, 2).HasFormula.Should().BeFalse();
        sessions.Cell(2, 2).Style.IncludeQuotePrefix.Should().BeTrue();
        sessions.Cell(2, 3).GetString().Should().Be("=SUM(1,1) Active");
        sessions.Cell(2, 3).HasFormula.Should().BeFalse();
        sessions.Cell(2, 3).Style.IncludeQuotePrefix.Should().BeTrue();
        sessions.Cell(2, 5).GetDateTime().Should().Be(new DateTime(2026, 8, 6, 0, 0, 0));
        sessions.Cell(2, 6).GetDateTime().Should().Be(new DateTime(2026, 8, 6, 1, 30, 0));
        sessions.Cell(3, 7).GetString().Should().Be("Active");
        sessions.Cell(3, 6).GetDateTime().Should().Be(new DateTime(2026, 8, 6, 12, 0, 0));

        var breaks = workbook.Worksheet("Breaks");
        breaks.LastRowUsed()!.RowNumber().Should().Be(3);
        breaks.Cell(3, 7).GetString().Should().Be("Active");
        breaks.Cell(3, 8).GetDouble().Should().Be(3_600);
    }

    [Fact]
    public async Task Team_export_contains_only_current_active_direct_reports_and_zero_hour_rows()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Employee.EmployeeNumber = "+EMP-1001";
        fixture.Employee.FirstName = "@Ada";
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 6, 0, 0));
        fixture.AddWork(
            fixture.InactiveEmployee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 6, 0, 0));
        fixture.AddWork(
            fixture.OtherEmployee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 6, 0, 0));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var file = await fixture.ServiceFor(fixture.Manager.Id).ExportTeamAsync(
            new TeamTimeLogExportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        file.FileName.Should().Be("team-time-logs-2026-08-06-to-2026-08-06.xlsx");
        using var workbook = Open(file);
        workbook.Worksheets.Select(x => x.Name).Should().Equal(
            "Summary", "Employees", "Work Sessions", "Breaks");
        var summary = workbook.Worksheet("Summary");
        SummaryNumber(summary, "Included Member Count").Should().Be(2);
        SummaryNumber(summary, "Excluded Inactive Count").Should().Be(1);
        SummaryNumber(summary, "Work Session Count").Should().Be(1);
        SummaryNumber(summary, "Total Worked Seconds").Should().Be(14_400);

        var employees = workbook.Worksheet("Employees");
        employees.LastRowUsed()!.RowNumber().Should().Be(3);
        FindEmployeeRow(employees, "+EMP-1001").Should().NotBe(0);
        var zeroRow = FindEmployeeRow(employees, fixture.ZeroHourEmployee.EmployeeNumber);
        zeroRow.Should().NotBe(0);
        employees.Cell(zeroRow, 6).GetDouble().Should().Be(0);
        employees.Cell(zeroRow, 9).GetDouble().Should().Be(0);
        FindEmployeeRow(employees, fixture.InactiveEmployee.EmployeeNumber).Should().Be(0);
        FindEmployeeRow(employees, fixture.OtherEmployee.EmployeeNumber).Should().Be(0);

        var formulaRow = FindEmployeeRow(employees, "+EMP-1001");
        employees.Cell(formulaRow, 2).HasFormula.Should().BeFalse();
        employees.Cell(formulaRow, 2).Style.IncludeQuotePrefix.Should().BeTrue();
        employees.Cell(formulaRow, 3).GetString().Should().Be("@Ada");
        employees.Cell(formulaRow, 3).HasFormula.Should().BeFalse();
    }

    [Fact]
    public async Task Team_export_can_include_inactive_current_direct_reports()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var file = await fixture.ServiceFor(fixture.Manager.Id).ExportTeamAsync(
            new TeamTimeLogExportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6),
                IncludeInactive = true
            },
            TestContext.Current.CancellationToken);

        using var workbook = Open(file);
        var summary = workbook.Worksheet("Summary");
        SummaryNumber(summary, "Included Member Count").Should().Be(3);
        SummaryNumber(summary, "Excluded Inactive Count").Should().Be(0);
        FindEmployeeRow(
            workbook.Worksheet("Employees"),
            fixture.InactiveEmployee.EmployeeNumber).Should().NotBe(0);
    }

    [Fact]
    public async Task Personal_export_excludes_deleted_sessions_and_breaks()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 20, 0),
            Utc(2026, 8, 5, 22, 0),
            new BreakSpec(
                Utc(2026, 8, 5, 20, 30),
                Utc(2026, 8, 5, 21, 30),
                IsDeleted: true));
        fixture.AddWork(
            fixture.Employee.Id,
            Utc(2026, 8, 5, 23, 0),
            Utc(2026, 8, 6, 1, 0)).IsDeleted = true;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var file = await fixture.ServiceFor(fixture.Employee.Id).ExportPersonalAsync(
            new TimeLogExportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        using var workbook = Open(file);
        var summary = workbook.Worksheet("Summary");
        SummaryNumber(summary, "Work Session Count").Should().Be(1);
        SummaryNumber(summary, "Break Count").Should().Be(0);
        SummaryNumber(summary, "Total Break Seconds").Should().Be(0);
        SummaryNumber(summary, "Total Worked Seconds").Should().Be(7_200);
    }

    [Fact]
    public async Task Personal_export_rechecks_that_the_current_account_is_active()
    {
        await using var fixture = await TestFixture.CreateAsync();
        fixture.Employee.IsActive = false;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var writer = new TrackingWorkbookWriter();

        var action = () => fixture.ServiceFor(fixture.Employee.Id, writer).ExportPersonalAsync(
            new TimeLogExportQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("USER_INACTIVE");
        writer.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task Team_export_rechecks_the_current_database_role()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.ServiceFor(fixture.Employee.Id).ExportTeamAsync(
            new TeamTimeLogExportQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("MANAGER_ACCESS_REQUIRED");
    }

    [Fact]
    public async Task Team_export_rejects_more_than_three_hundred_sixty_six_days()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.ServiceFor(fixture.Manager.Id).ExportTeamAsync(
            new TeamTimeLogExportQuery
            {
                StartDate = new DateOnly(2025, 8, 6),
                EndDate = new DateOnly(2026, 8, 7)
            },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("EXPORT_TOO_LARGE");
    }

    [Fact]
    public async Task Team_export_rejects_more_than_five_hundred_members_before_writing()
    {
        await using var fixture = await TestFixture.CreateAsync();
        for (var index = 0; index < 499; index++)
        {
            fixture.Db.Users.Add(TestFixture.User(
                2_000 + index,
                $"EMP-{2_000 + index}",
                UserRole.Employee,
                fixture.Manager));
        }
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var writer = new TrackingWorkbookWriter();

        var action = () => fixture.ServiceFor(fixture.Manager.Id, writer).ExportTeamAsync(
            new TeamTimeLogExportQuery(),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("EXPORT_TOO_LARGE");
        writer.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task Personal_export_rejects_more_than_ten_thousand_sessions_before_writing()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var rangeStart = Utc(2026, 8, 5, 16, 0);
        fixture.Db.TimeLogs.AddRange(Enumerable.Range(0, 10_001).Select(index => new TimeLog
        {
            UserId = fixture.Employee.Id,
            ShiftDate = new DateTime(2026, 8, 6),
            Start = rangeStart.AddSeconds(index * 2),
            End = rangeStart.AddSeconds(index * 2 + 1),
            Type = TimeLogType.Work,
            Timezone = "Asia/Manila",
            CreatedAt = rangeStart.AddSeconds(index * 2)
        }));
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var writer = new TrackingWorkbookWriter();

        var action = () => fixture.ServiceFor(fixture.Employee.Id, writer).ExportPersonalAsync(
            new TimeLogExportQuery
            {
                StartDate = new DateOnly(2026, 8, 6),
                EndDate = new DateOnly(2026, 8, 6)
            },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Code.Should().Be("EXPORT_TOO_LARGE");
        writer.WriteCount.Should().Be(0);
    }

    private static XLWorkbook Open(TimeLogExportFile file) => new(new MemoryStream(file.Contents));

    private static double SummaryNumber(IXLWorksheet sheet, string label)
    {
        var row = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1)
            .Single(x => sheet.Cell(x, 1).GetString() == label);
        return sheet.Cell(row, 2).GetDouble();
    }

    private static string SummaryText(IXLWorksheet sheet, string label)
    {
        var row = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1)
            .Single(x => sheet.Cell(x, 1).GetString() == label);
        return sheet.Cell(row, 2).GetString();
    }

    private static int FindEmployeeRow(IXLWorksheet sheet, string employeeNumber) =>
        Enumerable.Range(2, Math.Max(0, sheet.LastRowUsed()!.RowNumber() - 1))
            .SingleOrDefault(x => sheet.Cell(x, 2).GetString() == employeeNumber);

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed record BreakSpec(DateTime Start, DateTime? End, bool IsDeleted = false);

    private sealed class TrackingWorkbookWriter : ITimeLogWorkbookWriter
    {
        public int WriteCount { get; private set; }

        public TimeLogExportFile Write(TimeLogExportData data)
        {
            WriteCount++;
            return new([], "unused", "unused.xlsx");
        }
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly FixedTimeProvider _timeProvider = new(
            new DateTimeOffset(2026, 8, 6, 4, 0, 0, TimeSpan.Zero));

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            User manager,
            User employee,
            User zeroHourEmployee,
            User inactiveEmployee,
            User otherEmployee)
        {
            _connection = connection;
            Db = db;
            Manager = manager;
            Employee = employee;
            ZeroHourEmployee = zeroHourEmployee;
            InactiveEmployee = inactiveEmployee;
            OtherEmployee = otherEmployee;
        }

        public AppDbContext Db { get; }
        public User Manager { get; }
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
            manager.FirstName = "Mina";
            manager.LastName = "Manager";
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
                employee,
                zeroHourEmployee,
                inactiveEmployee,
                otherEmployee);
        }

        public TimeLogExportService ServiceFor(
            int userId,
            ITimeLogWorkbookWriter? writer = null)
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(userId);
            return new(
                Db,
                currentUser.Object,
                _timeProvider,
                writer ?? new TimeLogWorkbookWriter());
        }

        public TimeLog AddWork(
            int userId,
            DateTime start,
            DateTime? end,
            params BreakSpec[] breaks)
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
                    IsDeleted = item.IsDeleted,
                    CreatedAt = item.Start
                }).ToArray()
            };
            Db.TimeLogs.Add(work);
            return work;
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
                Email = $"{employeeId}@example.com",
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
