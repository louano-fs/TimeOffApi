using Microsoft.EntityFrameworkCore;
using TimeOffApi.Domain;

namespace TimeOffApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TimeLog> TimeLogs => Set<TimeLog>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.HasIndex(x => x.EmployeeId).IsUnique();
        user.HasIndex(x => x.EmployeeNumber).IsUnique();
        user.HasIndex(x => x.Email).IsUnique();
        user.Property(x => x.Email).HasMaxLength(320);
        user.Property(x => x.EmployeeNumber).HasMaxLength(50);
        user.Property(x => x.FirstName).HasMaxLength(100);
        user.Property(x => x.LastName).HasMaxLength(100);
        user.Property(x => x.Timezone).HasMaxLength(100);
        user.HasIndex(x => x.ManagerId);
        user.HasOne(x => x.Manager)
            .WithMany(x => x.DirectReports)
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        var request = modelBuilder.Entity<TimeOffRequest>();
        request.Property(x => x.StartDate).HasColumnType("date");
        request.Property(x => x.EndDate).HasColumnType("date");
        request.Property(x => x.Reason).HasMaxLength(500);
        request.HasIndex(x => new { x.UserId, x.Status });
        request.HasIndex(x => new { x.StartDate, x.EndDate });
        request.HasOne(x => x.User)
            .WithMany(x => x.TimeOffRequests)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        var timeLog = modelBuilder.Entity<TimeLog>();
        timeLog.Property(x => x.Timezone).HasMaxLength(100);
        timeLog.Property(x => x.ShiftDate).HasColumnType("date");
        timeLog.HasIndex(x => new { x.UserId, x.ShiftDate });
        timeLog.HasOne(x => x.User)
            .WithMany(x => x.TimeLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        timeLog.HasOne(x => x.ParentTimeLog)
            .WithMany(x => x.Breaks)
            .HasForeignKey(x => x.ParentTimeLogId)
            .OnDelete(DeleteBehavior.Restrict);

        var sqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var workFilter = sqlite
            ? "\"Type\" = 0 AND \"End\" IS NULL AND \"IsDeleted\" = 0"
            : "[Type] = 0 AND [End] IS NULL AND [IsDeleted] = 0";
        var breakFilter = sqlite
            ? "\"Type\" = 1 AND \"End\" IS NULL AND \"IsDeleted\" = 0"
            : "[Type] = 1 AND [End] IS NULL AND [IsDeleted] = 0";

        timeLog.HasIndex([nameof(TimeLog.UserId)], "UX_TimeLogs_OneActiveWorkPerUser")
            .IsUnique()
            .HasFilter(workFilter);
        timeLog.HasIndex([nameof(TimeLog.UserId)], "UX_TimeLogs_OneActiveBreakPerUser")
            .IsUnique()
            .HasFilter(breakFilter);
    }
}
