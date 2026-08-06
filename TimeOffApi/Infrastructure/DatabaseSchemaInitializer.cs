using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;

namespace TimeOffApi.Infrastructure;

public static class DatabaseSchemaInitializer
{
    public static async Task EnsureManagerSchemaAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'ManagerId'";
                var hasManagerId = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!hasManagerId)
                {
                    command.CommandText = "ALTER TABLE \"Users\" ADD COLUMN \"ManagerId\" INTEGER NULL";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                command.CommandText =
                    "CREATE INDEX IF NOT EXISTS \"IX_Users_ManagerId\" ON \"Users\" (\"ManagerId\")";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH(N'[Users]', N'ManagerId') IS NULL
                BEGIN
                    ALTER TABLE [Users] ADD [ManagerId] int NULL;
                    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_ManagerId]
                        FOREIGN KEY ([ManagerId]) REFERENCES [Users] ([Id]);
                    CREATE INDEX [IX_Users_ManagerId] ON [Users] ([ManagerId]);
                END
                """,
                cancellationToken);
        }
    }
}
