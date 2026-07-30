using System.Collections.Concurrent;

namespace TimeOffApi.Infrastructure;

public interface IUserLockService
{
    SemaphoreSlim GetLock(int userId);
}

public sealed class UserLockService : IUserLockService
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    public SemaphoreSlim GetLock(int userId) => _locks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
}
