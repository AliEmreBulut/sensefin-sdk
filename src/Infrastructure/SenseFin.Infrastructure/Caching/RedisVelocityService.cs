using SenseFin.Application.Interfaces;

namespace SenseFin.Infrastructure.Caching;

/// <summary>
/// Redis-backed velocity/rate-limiting service.
/// Delegates to RedisCacheService for atomic counter operations.
/// </summary>
public sealed class RedisVelocityService(RedisCacheService redisCacheService) : IVelocityService
{
    public async Task<long> IncrementAsync(string key, TimeSpan window)
    {
        return await redisCacheService.IncrementAsync(key, window);
    }
}
