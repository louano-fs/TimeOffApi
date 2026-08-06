using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeOffApi.Data;
using TimeOffApi.Domain;

namespace TimeOffApi.Infrastructure;

public static class DevelopmentDataSeeder
{
    private const string DefaultPassword = "Employee!2026";

    private static readonly string[] FirstNames =
    [
        "Adrian", "Aira", "Alex", "Andrea", "Angela", "Bea", "Carlo", "Celine", "Daniel", "Denise",
        "Ella", "Enzo", "Faith", "Gabriel", "Hazel", "Ian", "Jasmine", "Joshua", "Karen", "Kevin",
        "Lara", "Luis", "Mae", "Marco", "Mia", "Nathan", "Nicole", "Paolo", "Patricia", "Rafael",
        "Rhea", "Rico", "Samantha", "Sean", "Sofia", "Tristan", "Valerie", "Victor", "Yana", "Zach",
        "Bianca", "Christian", "Diana", "Francis", "Grace", "Jerome", "Kim", "Miguel", "Nina", "Oliver"
    ];

    private static readonly string[] LastNames =
    [
        "Santos", "Reyes", "Cruz", "Garcia", "Mendoza", "Torres", "Flores", "Ramos", "Aquino", "Castillo"
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()
            || !configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
            return;

        var db = services.GetRequiredService<AppDbContext>();
        await EnsureTimeOffRequestSchemaAsync(db);
        var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();
        var createdAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc);

        var admin = await db.Users.SingleOrDefaultAsync(x => x.Email == "admin@timeclock.local");
        if (admin is null)
        {
            admin = new User
            {
                EmployeeId = 9000,
                EmployeeNumber = "ADMIN-DEV",
                Email = "admin@timeclock.local",
                FirstName = "Development",
                LastName = "Administrator",
                Role = UserRole.Administrator,
                Timezone = "Asia/Manila",
                IsActive = true,
                CreatedAt = createdAt
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, DefaultPassword);
            db.Users.Add(admin);
        }

        var manager = await db.Users.SingleOrDefaultAsync(x => x.Email == "manager@timeclock.local");
        if (manager is null)
        {
            manager = new User
            {
                EmployeeId = 8000,
                EmployeeNumber = "MGR-DEV",
                Email = "manager@timeclock.local",
                FirstName = "Morgan",
                LastName = "Manager",
                Role = UserRole.Manager,
                Timezone = "Asia/Manila",
                IsActive = true,
                CreatedAt = createdAt
            };
            manager.PasswordHash = passwordHasher.HashPassword(manager, DefaultPassword);
            db.Users.Add(manager);
            await db.SaveChangesAsync();
        }

        for (var index = 0; index < 50; index++)
        {
            var employeeId = 1001 + index;
            var employeeNumber = $"EMP-{employeeId}";
            if (await db.Users.AnyAsync(x => x.EmployeeNumber == employeeNumber))
                continue;

            var user = new User
            {
                EmployeeId = employeeId,
                EmployeeNumber = employeeNumber,
                Email = $"employee{index + 1:00}@timeclock.local",
                FirstName = FirstNames[index],
                LastName = LastNames[index % LastNames.Length],
                Role = UserRole.Employee,
                Timezone = "Asia/Manila",
                IsActive = true,
                CreatedAt = createdAt.AddMinutes(index)
            };
            user.PasswordHash = passwordHasher.HashPassword(user, DefaultPassword);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();

        var teamMembers = await db.Users
            .Where(x => x.EmployeeId >= 1001 && x.EmployeeId <= 1010 && x.ManagerId == null)
            .ToListAsync();
        foreach (var teamMember in teamMembers)
            teamMember.ManagerId = manager.Id;

        await db.SaveChangesAsync();

        var requests = new[]
        {
            new SeedRequest(1003, new DateTime(2026, 8, 3), new DateTime(2026, 8, 4),
                TimeOffType.Vacation, "Family trip"),
            new SeedRequest(1007, new DateTime(2026, 8, 7), new DateTime(2026, 8, 7),
                TimeOffType.Personal, "Personal appointment"),
            new SeedRequest(1012, new DateTime(2026, 8, 10), new DateTime(2026, 8, 12),
                TimeOffType.Vacation, "Out-of-town vacation"),
            new SeedRequest(1018, new DateTime(2026, 8, 14), new DateTime(2026, 8, 14),
                TimeOffType.Sick, "Medical checkup"),
            new SeedRequest(1025, new DateTime(2026, 8, 17), new DateTime(2026, 8, 18),
                TimeOffType.Personal, "Family commitment"),
            new SeedRequest(1031, new DateTime(2026, 8, 21), new DateTime(2026, 8, 21),
                TimeOffType.Vacation, "Long weekend"),
            new SeedRequest(1042, new DateTime(2026, 8, 24), new DateTime(2026, 8, 26),
                TimeOffType.Vacation, "Annual leave")
        };

        foreach (var seed in requests)
        {
            var user = await db.Users.SingleAsync(x => x.EmployeeId == seed.EmployeeId);
            var exists = await db.TimeOffRequests.AnyAsync(x =>
                x.UserId == user.Id
                && x.StartDate == seed.StartDate
                && x.EndDate == seed.EndDate
                && x.Type == seed.Type);
            if (exists)
                continue;

            db.TimeOffRequests.Add(new TimeOffRequest
            {
                UserId = user.Id,
                StartDate = seed.StartDate,
                EndDate = seed.EndDate,
                Type = seed.Type,
                Status = TimeOffRequestStatus.Pending,
                Reason = seed.Reason,
                CreatedAt = createdAt.AddDays(-1).AddMinutes(seed.EmployeeId - 1000)
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureTimeOffRequestSchemaAsync(AppDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "TimeOffRequests" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TimeOffRequests" PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL,
                    "StartDate" date NOT NULL,
                    "EndDate" date NOT NULL,
                    "Type" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "Reason" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_TimeOffRequests_Users_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS "IX_TimeOffRequests_UserId_Status"
                    ON "TimeOffRequests" ("UserId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_TimeOffRequests_StartDate_EndDate"
                    ON "TimeOffRequests" ("StartDate", "EndDate");
                """);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[TimeOffRequests]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TimeOffRequests] (
                        [Id] int NOT NULL IDENTITY,
                        [UserId] int NOT NULL,
                        [StartDate] date NOT NULL,
                        [EndDate] date NOT NULL,
                        [Type] int NOT NULL,
                        [Status] int NOT NULL,
                        [Reason] nvarchar(500) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_TimeOffRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_TimeOffRequests_Users_UserId]
                            FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
                    );
                    CREATE INDEX [IX_TimeOffRequests_UserId_Status]
                        ON [TimeOffRequests] ([UserId], [Status]);
                    CREATE INDEX [IX_TimeOffRequests_StartDate_EndDate]
                        ON [TimeOffRequests] ([StartDate], [EndDate]);
                END
                """);
        }
    }

    private sealed record SeedRequest(
        int EmployeeId,
        DateTime StartDate,
        DateTime EndDate,
        TimeOffType Type,
        string Reason);
}
