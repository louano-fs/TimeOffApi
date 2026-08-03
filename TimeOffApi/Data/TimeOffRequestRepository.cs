using Microsoft.EntityFrameworkCore;
using TimeOffApi.Domain;

namespace TimeOffApi.Data;

public interface ITimeOffRequestRepository
{
    Task<TimeOffRequest?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> TryChangeStatusAsync(
        int id,
        TimeOffRequestStatus expectedStatus,
        TimeOffRequestStatus newStatus,
        CancellationToken cancellationToken);
}

public sealed class TimeOffRequestRepository(AppDbContext db) : ITimeOffRequestRepository
{
    public Task<TimeOffRequest?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.TimeOffRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> TryChangeStatusAsync(
        int id,
        TimeOffRequestStatus expectedStatus,
        TimeOffRequestStatus newStatus,
        CancellationToken cancellationToken)
    {
        var affectedRows = await db.TimeOffRequests
            .Where(x => x.Id == id && x.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, newStatus),
                cancellationToken);

        return affectedRows == 1;
    }
}
