using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Infrastructure;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class DatabaseSchemaInitializerTests
{
    [Fact]
    public async Task EnsureManagerSchema_adds_the_manager_column_to_an_existing_sqlite_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Users (Id INTEGER PRIMARY KEY)";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);

        await DatabaseSchemaInitializer.EnsureManagerSchemaAsync(
            db,
            TestContext.Current.CancellationToken);
        await DatabaseSchemaInitializer.EnsureManagerSchemaAsync(
            db,
            TestContext.Current.CancellationToken);

        await using var verification = connection.CreateCommand();
        verification.CommandText =
            "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'ManagerId'";
        Convert.ToInt32(
            await verification.ExecuteScalarAsync(TestContext.Current.CancellationToken))
            .Should().Be(1);
    }
}
