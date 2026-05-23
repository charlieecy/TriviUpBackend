using Microsoft.Extensions.Caching.StackExchangeRedis;
using System.Text.Json;

namespace TriviUpBackend.Services.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            return data is null ? default : JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var data = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
            await _cache.SetStringAsync(key, data, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try { await _cache.RemoveAsync(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache remove failed for key {Key}", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        // Redis SCAN + DEL para prefijos - o guarda prefijos en un Set
        // Por simplicidad, marca invalidación en logs
        _logger.LogInformation("Cache invalidation requested for prefix: {Prefix}", prefix);
    }
}