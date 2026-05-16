using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace SenseFin.Infrastructure.Caching;

// Redis üzerinde basit key-value işlemlerini yöneten servis.
public sealed class RedisCacheService : IDisposable
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisCacheService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    // Belirtilen key'e karşılık gelen değeri getirir
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key '{Key}'.", key);
            return null;
        }
    }

    // Key ve value değerini Redis'e yazar, opsiyonel olarak süre (TTL) verilebilir
    public async Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.StringSetAsync(key, value, expiry);
            _logger.LogDebug("Redis SET key '{Key}' (TTL: {Expiry}).", key, expiry?.ToString() ?? "none");
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key '{Key}'.", key);
        }
    }

    // Kaydı Redis'ten siler
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Redis DELETE key '{Key}'.", key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis DELETE failed for key '{Key}'.", key);
        }
    }

    // Key var mı yok mu kontrol eder
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for key '{Key}'.", key);
            return false;
        }
    }

    // Hash yapısında bir alan (field) günceller (kompleks nesneler için ideal)
    public async Task HashSetAsync(string key, string field, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.HashSetAsync(key, field, value);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis HSET failed for key '{Key}', field '{Field}'.", key, field);
        }
    }

    // Hash alanındaki değeri çeker
    public async Task<string?> HashGetAsync(string key, string field, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _database.HashGetAsync(key, field);
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis HGET failed for key '{Key}', field '{Field}'.", key, field);
            return null;
        }
    }

    // Sayacı artırır. Key yeni oluşturuluyorsa süre tanımlar.
    // Özellikle limit (velocity) kontrollerinde kullanılır.
    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        try
        {
            var newValue = await _database.StringIncrementAsync(key);

            // Set expiry only when the counter is first created (value == 1)
            if (newValue == 1 && expiry.HasValue)
            {
                await _database.KeyExpireAsync(key, expiry.Value);
            }

            _logger.LogDebug("Redis INCREMENT key '{Key}' = {Value}.", key, newValue);
            return newValue;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis INCREMENT failed for key '{Key}'.", key);
            return 0;
        }
    }

    public void Dispose()
    {
        _connectionMultiplexer?.Dispose();
    }
}
