using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TimeOffApi.Controllers;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class ManagerAssistantCapabilitiesTests
{
    [Fact]
    public void Capability_endpoint_requires_authentication_without_a_manager_role_policy()
    {
        var authorize = typeof(ManagerAssistantCapabilitiesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().BeNull();
    }

    [Fact]
    public async Task Enabled_feature_returns_manager_direct_report_capability()
    {
        await using var fixture = await TestFixture.CreateAsync(
            UserRole.Manager,
            isActive: true,
            tokenHasManagerRole: true,
            featureEnabled: true);

        var result = await fixture.Capabilities.GetAsync(
            TestContext.Current.CancellationToken);

        result.Enabled.Should().BeTrue();
        result.Audience.Should().Be("Manager");
        result.Scope.Should().Be("directReports");
        result.Streaming.Should().BeFalse();
        result.MaxMessageLength.Should().Be(1_000);
    }

    [Theory]
    [InlineData(UserRole.Employee, true, true)]
    [InlineData(UserRole.Administrator, true, true)]
    [InlineData(UserRole.Manager, false, true)]
    [InlineData(UserRole.Manager, true, false)]
    public async Task Ineligible_callers_receive_a_disabled_capability(
        UserRole databaseRole,
        bool isActive,
        bool tokenHasManagerRole)
    {
        await using var fixture = await TestFixture.CreateAsync(
            databaseRole,
            isActive,
            tokenHasManagerRole,
            featureEnabled: true);

        var result = await fixture.Capabilities.GetAsync(
            TestContext.Current.CancellationToken);

        result.Enabled.Should().BeFalse();
        result.Audience.Should().BeNull();
        result.Scope.Should().BeNull();
    }

    [Fact]
    public async Task Disabled_feature_stays_hidden_from_an_eligible_manager()
    {
        await using var fixture = await TestFixture.CreateAsync(
            UserRole.Manager,
            isActive: true,
            tokenHasManagerRole: true,
            featureEnabled: false);

        var result = await fixture.Capabilities.GetAsync(
            TestContext.Current.CancellationToken);

        result.Enabled.Should().BeFalse();
        result.Audience.Should().BeNull();
        result.Scope.Should().BeNull();
    }

    [Fact]
    public async Task Capability_stays_disabled_until_a_live_model_adapter_is_available()
    {
        await using var fixture = await TestFixture.CreateAsync(
            UserRole.Manager,
            isActive: true,
            tokenHasManagerRole: true,
            featureEnabled: true);
        var service = new ManagerAssistantCapabilitiesService(
            fixture.ScopeResolver,
            Options.Create(new ManagerAssistantOptions
            {
                Enabled = true,
                Provider = "Pending",
                Model = "pending-model"
            }),
            new UnconfiguredAssistantModelClient());

        var result = await service.GetAsync(TestContext.Current.CancellationToken);

        result.Enabled.Should().BeFalse();
        result.Scope.Should().BeNull();
    }

    [Fact]
    public async Task Required_scope_captures_server_identity_timezone_and_one_as_of_instant()
    {
        await using var fixture = await TestFixture.CreateAsync(
            UserRole.Manager,
            isActive: true,
            tokenHasManagerRole: true,
            featureEnabled: true);

        var result = await fixture.ScopeResolver.ResolveRequiredAsync(
            TestContext.Current.CancellationToken);

        result.ManagerId.Should().Be(fixture.User.Id);
        result.Timezone.Should().Be("Asia/Manila");
        result.AsOf.Should().Be(new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Required_scope_rechecks_the_database_role_on_every_call()
    {
        await using var fixture = await TestFixture.CreateAsync(
            UserRole.Manager,
            isActive: true,
            tokenHasManagerRole: true,
            featureEnabled: true);
        fixture.User.Role = UserRole.Employee;
        await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => fixture.ScopeResolver.ResolveRequiredAsync(
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ForbiddenException>();
        exception.Which.Code.Should().Be("MANAGER_ACCESS_REQUIRED");
    }

    [Fact]
    public void Configuration_is_disabled_by_default_and_requires_provider_settings_when_enabled()
    {
        var defaults = new ManagerAssistantOptions();
        var incompleteEnabled = new ManagerAssistantOptions { Enabled = true };
        var completeEnabled = new ManagerAssistantOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "tool-capable-model"
        };
        var oversized = new ManagerAssistantOptions { MaxTeamMembers = 201 };

        defaults.Enabled.Should().BeFalse();
        ManagerAssistantOptions.HasValidLimits(defaults).Should().BeTrue();
        ManagerAssistantOptions.HasValidLimits(oversized).Should().BeFalse();
        ManagerAssistantOptions.HasRequiredProviderSettings(incompleteEnabled).Should().BeFalse();
        ManagerAssistantOptions.HasRequiredProviderSettings(completeEnabled).Should().BeTrue();
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext db,
            User user,
            ManagerScopeResolver scopeResolver,
            ManagerAssistantCapabilitiesService capabilities)
        {
            _connection = connection;
            Db = db;
            User = user;
            ScopeResolver = scopeResolver;
            Capabilities = capabilities;
        }

        public AppDbContext Db { get; }
        public User User { get; }
        public ManagerScopeResolver ScopeResolver { get; }
        public ManagerAssistantCapabilitiesService Capabilities { get; }

        public static async Task<TestFixture> CreateAsync(
            UserRole databaseRole,
            bool isActive,
            bool tokenHasManagerRole,
            bool featureEnabled)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var user = new User
            {
                EmployeeId = 8_000,
                EmployeeNumber = "MGR-8000",
                Email = "manager@example.com",
                PasswordHash = "not-used",
                FirstName = "Manny",
                LastName = "Manager",
                Role = databaseRole,
                Timezone = "Asia/Manila",
                IsActive = isActive,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(user.Id);
            currentUser.Setup(x => x.IsInRole(UserRole.Manager)).Returns(tokenHasManagerRole);
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 6, 4, 0, 0, TimeSpan.Zero));
            var scopeResolver = new ManagerScopeResolver(db, currentUser.Object, timeProvider);
            var managerOptions = Options.Create(new ManagerAssistantOptions
            {
                Enabled = featureEnabled,
                Provider = featureEnabled ? "OpenAI" : string.Empty,
                Model = featureEnabled ? "tool-capable-model" : string.Empty
            });

            return new(
                connection,
                db,
                user,
                scopeResolver,
                new ManagerAssistantCapabilitiesService(
                    scopeResolver,
                    managerOptions,
                    new AvailableModel()));
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

    private sealed class AvailableModel : IAssistantModelAvailability
    {
        public bool IsAvailable => true;
    }
}
