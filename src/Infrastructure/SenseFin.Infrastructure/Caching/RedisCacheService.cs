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
    // LUA Script ile atomik (Race-Condition korumalı) hale getirilmiştir.
    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Eğer süre (TTL) verilmediyse düz C# artırma yapabiliriz
            if (!expiry.HasValue)
            {
                return await _database.StringIncrementAsync(key);
            }

            // Claude'un önerdiği Atomik Lua Scripti
            const string script = @"
                local current = redis.call('INCR', KEYS[1])
                if current == 1 then
                    redis.call('EXPIRE', KEYS[1], ARGV[1])
                end
                return current";

            var result = await _database.ScriptEvaluateAsync(script, 
                new RedisKey[] { key }, 
                new RedisValue[] { (int)expiry.Value.TotalSeconds });

            long newValue = (long)result;
            _logger.LogDebug("Redis LUA INCREMENT key '{Key}' = {Value} (TTL: {Seconds}s).", key, newValue, expiry.Value.TotalSeconds);
            
            return newValue;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis LUA INCREMENT failed for key '{Key}'.", key);
            return 0; // Hata durumunda 0 dönerek fail-safe sağlar
        }
    }
   

    public void Dispose()
    {
        _connectionMultiplexer?.Dispose();
    }
}
