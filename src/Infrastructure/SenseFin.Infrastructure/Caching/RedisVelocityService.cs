using SenseFin.Application.Interfaces;

namespace SenseFin.Infrastructure.Caching;

// Redis destekli limit (velocity) kontrol servisi.
// Sayaç işlemleri için RedisCacheService'i kullanır.
public sealed class RedisVelocityService(RedisCacheService redisCacheService) : IVelocityService
{
    public async Task<long> IncrementAsync(string key, TimeSpan window)
    {
        return await redisCacheService.IncrementAsync(key, window);
    }
}
