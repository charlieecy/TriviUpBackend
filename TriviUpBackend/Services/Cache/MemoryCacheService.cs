using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;

namespace TriviUpBackend.Services.Cache;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
        _logger.LogInformation("[MemoryCacheService] Service initialized");
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            _logger.LogDebug("[MemoryCacheService] GetAsync called for key: {Key}", key);
            if (_cache.TryGetValue(key, out var value))
            {
                _logger.LogDebug("[MemoryCacheService] Cache HIT for key: {Key}", key);
                return (T?)value;
            }
            _logger.LogDebug("[MemoryCacheService] Cache MISS for key: {Key}", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryCacheService] Cache get FAILED for key {Key}. Error: {Error}", key, ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            _logger.LogDebug("[MemoryCacheService] SetAsync called for key: {Key}, expiry: {Expiry}", key, expiry);
            var options = new MemoryCacheEntryOptions();
            if (expiry.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiry;
            }
            _cache.Set(key, value, options);
            _logger.LogDebug("[MemoryCacheService] Cache SET success for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryCacheService] Cache set FAILED for key {Key}. Error: {Error}", key, ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _logger.LogDebug("[MemoryCacheService] RemoveAsync called for key: {Key}", key);
            _cache.Remove(key);
            _logger.LogDebug("[MemoryCacheService] Cache REMOVE success for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryCacheService] Cache remove FAILED for key {Key}. Error: {Error}", key, ex.Message);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        // IMemoryCache no soporta busqueda por prefijo, marcar en logs
        _logger.LogInformation("[MemoryCacheService] Cache invalidation requested for prefix: {Prefix} (not supported in memory cache)", prefix);
    }
}