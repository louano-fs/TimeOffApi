using FluentAssertions;
using Moq;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;
using Xunit;

namespace TimeOffApi.Tests;

public sealed class TimeOffRequestServiceTests
{
    [Fact]
    public async Task ApproveAsync_changes_a_pending_request_to_approved()
    {
        var request = CreateRequest(TimeOffRequestStatus.Pending);
        var repository = CreateRepository(request);
        var service = new TimeOffRequestService(repository.Object);

        var result = await service.ApproveAsync(
            request.Id,
            TestContext.Current.CancellationToken);

        request.Status.Should().Be(TimeOffRequestStatus.Approved);
        result.Status.Should().Be(nameof(TimeOffRequestStatus.Approved));
        repository.Verify(
            x => x.TryChangeStatusAsync(
                request.Id,
                TimeOffRequestStatus.Pending,
                TimeOffRequestStatus.Approved,
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task RejectAsync_changes_a_pending_request_to_rejected()
    {
        var request = CreateRequest(TimeOffRequestStatus.Pending);
        var repository = CreateRepository(request);
        var service = new TimeOffRequestService(repository.Object);

        var result = await service.RejectAsync(
            request.Id,
            TestContext.Current.CancellationToken);

        request.Status.Should().Be(TimeOffRequestStatus.Rejected);
        result.Status.Should().Be(nameof(TimeOffRequestStatus.Rejected));
        repository.Verify(
            x => x.TryChangeStatusAsync(
                request.Id,
                TimeOffRequestStatus.Pending,
                TimeOffRequestStatus.Rejected,
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_rejects_an_already_rejected_request()
    {
        var request = CreateRequest(TimeOffRequestStatus.Rejected);
        var repository = CreateRepository(request);
        var service = new TimeOffRequestService(repository.Object);

        var action = () => service.ApproveAsync(
            request.Id,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("TIME_OFF_REQUEST_NOT_PENDING");
        request.Status.Should().Be(TimeOffRequestStatus.Rejected);
        repository.Verify(
            x => x.TryChangeStatusAsync(
                It.IsAny<int>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_throws_when_the_request_does_not_exist()
    {
        var repository = CreateRepository(null);
        var service = new TimeOffRequestService(repository.Object);

        var action = () => service.ApproveAsync(
            404,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.Code.Should().Be("TIME_OFF_REQUEST_NOT_FOUND");
        repository.Verify(
            x => x.TryChangeStatusAsync(
                It.IsAny<int>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<ITimeOffRequestRepository> CreateRepository(TimeOffRequest? request)
    {
        var repository = new Mock<ITimeOffRequestRepository>();
        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repository
            .Setup(x => x.TryChangeStatusAsync(
                It.IsAny<int>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<TimeOffRequestStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return repository;
    }

    private static TimeOffRequest CreateRequest(TimeOffRequestStatus status) =>
        new()
        {
            Id = 17,
            UserId = 3,
            StartDate = new DateTime(2026, 8, 3),
            EndDate = new DateTime(2026, 8, 4),
            Type = TimeOffType.Vacation,
            Status = status,
            Reason = "Family trip",
            CreatedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
        };
}
