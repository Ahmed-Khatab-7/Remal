using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Remal.Application.Common.Interfaces;

namespace Remal.Infrastructure.Services;

/// <summary>
/// In-memory cache implementation. Tracks keys so we can RemoveByPrefix without scanning IMemoryCache internals.
/// Swap with a Redis-based implementation behind ICacheService when scale demands.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache; _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var opts = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl,
            Size = 1,
        };
        opts.RegisterPostEvictionCallback((k, _, _, _) => _keys.TryRemove((string)k, out _));
        _cache.Set(key, value, opts);
        _keys[key] = 0;
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null) return cached;
        var value = await factory(ct);
        await SetAsync(key, value, ttl, ct);
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keys = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var k in keys)
        {
            _cache.Remove(k);
            _keys.TryRemove(k, out _);
        }
        _logger.LogDebug("Cache invalidated {Count} keys with prefix '{Prefix}'", keys.Count, prefix);
        return Task.CompletedTask;
    }
}
