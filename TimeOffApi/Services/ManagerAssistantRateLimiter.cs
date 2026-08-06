using System.Collections.Concurrent;

namespace TimeOffApi.Services;

public interface IManagerAssistantRateLimiter
{
    bool TryAcquire(int managerId);
}

public sealed class ManagerAssistantRateLimiter(TimeProvider timeProvider)
    : IManagerAssistantRateLimiter
{
    private const int PermitLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<int, Queue<DateTimeOffset>> _requests = new();

    public bool TryAcquire(int managerId)
    {
        var now = timeProvider.GetUtcNow();
        var requests = _requests.GetOrAdd(managerId, _ => new());
        lock (requests)
        {
            while (requests.TryPeek(out var requestedAt)
                && (requestedAt > now || now - requestedAt >= Window))
                requests.Dequeue();
            if (requests.Count >= PermitLimit)
                return false;

            requests.Enqueue(now);
            return true;
        }
    }
}
