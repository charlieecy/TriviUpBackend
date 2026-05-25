using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace TriviUpBackend.Services.Cache;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _keysByPrefix = new();

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

            // Track key by prefixes (extract prefixes from key)
            TrackKeyByPrefixes(key);

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
            RemoveKeyFromPrefixTracking(key);
            _logger.LogDebug("[MemoryCacheService] Cache REMOVE success for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryCacheService] Cache remove FAILED for key {Key}. Error: {Error}", key, ex.Message);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        try
        {
            _logger.LogInformation("[MemoryCacheService] RemoveByPrefixAsync called for prefix: {Prefix}", prefix);

            if (_keysByPrefix.TryGetValue(prefix, out var keys))
            {
                var keysToRemove = keys.ToList();
                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    _logger.LogDebug("[MemoryCacheService] Removed key: {Key} for prefix: {Prefix}", key, prefix);
                }
                _keysByPrefix.TryRemove(prefix, out _);
                _logger.LogInformation("[MemoryCacheService] Removed {Count} keys for prefix: {Prefix}", keysToRemove.Count, prefix);
            }
            else
            {
                _logger.LogDebug("[MemoryCacheService] No keys found for prefix: {Prefix}", prefix);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryCacheService] RemoveByPrefixAsync FAILED for prefix {Prefix}. Error: {Error}", prefix, ex.Message);
        }
    }

    private void TrackKeyByPrefixes(string key)
    {
        // Track the key under all possible prefixes
        // For "quizzes:public:search:1:10", track prefixes:
        // "quizzes:public:", "quizzes:", "quizzes"
        var parts = key.Split(':');
        for (int i = 1; i <= parts.Length; i++)
        {
            var prefix = string.Join(":", parts.Take(i)) + ":";
            _keysByPrefix.AddOrUpdate(
                prefix,
                _ => new HashSet<string> { key },
                (_, set) => { set.Add(key); return set; }
            );
        }
    }

    private void RemoveKeyFromPrefixTracking(string key)
    {
        var parts = key.Split(':');
        for (int i = 1; i <= parts.Length; i++)
        {
            var prefix = string.Join(":", parts.Take(i)) + ":";
            if (_keysByPrefix.TryGetValue(prefix, out var set))
            {
                set.Remove(key);
            }
        }
    }
}