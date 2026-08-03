using TimeOffApi.Contracts;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public interface ITimeOffRequestService
{
    Task<TimeOffDecisionResponse> ApproveAsync(int id, CancellationToken cancellationToken);
    Task<TimeOffDecisionResponse> RejectAsync(int id, CancellationToken cancellationToken);
}

public sealed class TimeOffRequestService(ITimeOffRequestRepository repository)
    : ITimeOffRequestService
{
    public Task<TimeOffDecisionResponse> ApproveAsync(
        int id,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, TimeOffRequestStatus.Approved, cancellationToken);

    public Task<TimeOffDecisionResponse> RejectAsync(
        int id,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, TimeOffRequestStatus.Rejected, cancellationToken);

    private async Task<TimeOffDecisionResponse> ChangeStatusAsync(
        int id,
        TimeOffRequestStatus newStatus,
        CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(
                "TIME_OFF_REQUEST_NOT_FOUND",
                "Time-off request was not found.");

        if (request.Status != TimeOffRequestStatus.Pending)
            throw new ConflictException(
                "TIME_OFF_REQUEST_NOT_PENDING",
                "Only pending time-off requests can be approved or rejected.");

        var changed = await repository.TryChangeStatusAsync(
            request.Id,
            TimeOffRequestStatus.Pending,
            newStatus,
            cancellationToken);
        if (!changed)
            throw new ConflictException(
                "TIME_OFF_REQUEST_NOT_PENDING",
                "Only pending time-off requests can be approved or rejected.");

        request.Status = newStatus;
        return new TimeOffDecisionResponse(request.Id, newStatus.ToString());
    }
}
