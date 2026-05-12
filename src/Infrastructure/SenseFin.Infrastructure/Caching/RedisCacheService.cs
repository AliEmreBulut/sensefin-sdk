using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace SenseFin.Infrastructure.Caching;

/// <summary>
/// Redis cache service providing basic key-value operations.
/// Uses StackExchange.Redis for connection management.
/// </summary>
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

    /// <summary>
    /// Gets a cached string value by key.
    /// </summary>
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

    /// <summary>
    /// Sets a cached string value with optional TTL.
    /// </summary>
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

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
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

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
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

    /// <summary>
    /// Sets a hash field value (useful for caching complex objects).
    /// </summary>
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

    /// <summary>
    /// Gets a hash field value.
    /// </summary>
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

    /// <summary>
    /// Atomically increments a counter key. If the key is new, sets the expiry.
    /// Used for velocity/rate-limiting checks.
    /// </summary>
    /// <param name="key">The counter key.</param>
    /// <param name="expiry">TTL for the key (only set on first increment).</param>
    /// <returns>The value of the counter after incrementing.</returns>
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
