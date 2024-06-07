using StackExchange.Redis.Extensions.Core.Abstractions;

namespace GameRepository.Repositories;

public interface ISessionRepository
{
    ValueTask<bool> ValidateSessionAsync(string userId, string sessionId);

    ValueTask SetSessionAsync(string userId, string sessionId);

    ValueTask RemoveSessionAsync(string userId);
}

sealed class SessionRepository(IRedisClientFactory factory) : ISessionRepository
{
    const string KeyPrefix = "user-session_";
    static readonly TimeSpan CommonExpire = TimeSpan.FromMinutes(10);

    readonly IRedisDatabase _db = factory.GetDefaultRedisClient().Db0;

    public async ValueTask SetSessionAsync(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = KeyPrefix + userId;
        await _db.AddAsync(key, sessionId, expiresIn: CommonExpire);
    }

    public async ValueTask<bool> ValidateSessionAsync(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var key = KeyPrefix + userId;
        return await _db.GetAsync<string>(key) == sessionId;
    }

    public async ValueTask RemoveSessionAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var key = KeyPrefix + userId;
        await _db.RemoveAsync(key);
    }
}
