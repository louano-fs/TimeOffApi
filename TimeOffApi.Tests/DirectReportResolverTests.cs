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

public sealed class DirectReportResolverTests
{
    [Theory]
    [InlineData(" emp-1001 ")]
    [InlineData("  ADA   ACTIVE ")]
    [InlineData("ada")]
    public async Task Resolves_exact_authorized_employee_references(string employeeReference)
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Resolver.ResolveAsync(
            fixture.Scope,
            employeeReference,
            includeInactive: false,
            TestContext.Current.CancellationToken);

        result.Should().BeOfType<ResolvedDirectReport>()
            .Which.Member.EmployeeNumber.Should().Be("EMP-1001");
    }

    [Fact]
    public async Task Duplicate_exact_first_names_return_only_current_team_candidates()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var result = await fixture.Resolver.ResolveAsync(
            fixture.Scope,
            "Sam",
            includeInactive: false,
            TestContext.Current.CancellationToken);

        var candidates = result.Should().BeOfType<AmbiguousDirectReport>().Which.Candidates;
        candidates.Should().HaveCount(2);
        candidates.Select(x => x.EmployeeNumber)
            .Should().BeEquivalentTo("EMP-1002", "EMP-1003");
    }

    [Theory]
    [InlineData("Act")]
    [InlineData("OUT-9001")]
    [InlineData("Missing Person")]
    public async Task Substrings_outside_team_and_missing_references_share_not_found(
        string employeeReference)
    {
        await using var fixture = await TestFixture.CreateAsync();

        var action = () => fixture.Resolver.ResolveAsync(
            fixture.Scope,
            employeeReference,
            includeInactive: false,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("TEAM_MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Inactive_direct_reports_require_explicit_inclusion()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var excluded = () => fixture.Resolver.ResolveAsync(
            fixture.Scope,
            "EMP-1004",
            includeInactive: false,
            TestContext.Current.CancellationToken);
        var included = await fixture.Resolver.ResolveAsync(
            fixture.Scope,
            "EMP-1004",
            includeInactive: true,
            TestContext.Current.CancellationToken);

        (await excluded.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("TEAM_MEMBER_NOT_FOUND");
        included.Should().BeOfType<ResolvedDirectReport>()
            .Which.Member.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Reassignment_changes_resolution_immediately()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var employee = await fixture.Db.Users.SingleAsync(
            x => x.EmployeeNumber == "EMP-1001",
            TestContext.Current.CancellationToken);
        var otherManager = await fixture.Db.Users.SingleAsync(
            x => x.EmployeeNumber == "MGR-8001",
            TestContext.Current.CancellationToken);
        employee.ManagerId = otherManager.Id;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => fixture.Resolver.ResolveAsync(
            fixture.Scope,
            "EMP-1001",
            includeInactive: false,
            TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<NotFoundException>())
            .Which.Code.Should().Be("TEAM_MEMBER_NOT_FOUND");
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            DirectReportResolver resolver,
            ManagerScope scope)
        {
            _connection = connection;
            Db = db;
            Resolver = resolver;
            Scope = scope;
        }

        public AppDbContext Db { get; }
        public DirectReportResolver Resolver { get; }
        public ManagerScope Scope { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var manager = User(8_000, "MGR-8000", "Manny", "Manager", UserRole.Manager);
            var otherManager = User(8_001, "MGR-8001", "Other", "Manager", UserRole.Manager);
            db.Users.AddRange(
                manager,
                otherManager,
                User(1_001, "EMP-1001", "Ada", "Active", UserRole.Employee, manager),
                User(1_002, "EMP-1002", "Sam", "First", UserRole.Employee, manager),
                User(1_003, "EMP-1003", "Sam", "Second", UserRole.Employee, manager),
                User(1_004, "EMP-1004", "Ina", "Inactive", UserRole.Employee, manager, false),
                User(9_001, "OUT-9001", "Outside", "Employee", UserRole.Employee, otherManager));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            return new(
                connection,
                db,
                new DirectReportResolver(db, Options.Create(new ManagerAssistantOptions())),
                new ManagerScope(
                    manager.Id,
                    "Asia/Manila",
                    new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Utc)));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

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
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            };
    }
}
