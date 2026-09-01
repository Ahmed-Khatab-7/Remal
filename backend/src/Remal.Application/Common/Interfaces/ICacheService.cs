namespace Remal.Application.Common.Interfaces;

/// <summary>
/// Abstraction so we can swap IMemoryCache → Redis with a single DI line later.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

public static class CacheKeys
{
    public const string FeaturedProducts = "products:featured";
    public const string PublicSettings = "settings:public";
    public const string AllBundles = "bundles:all";
    public const string AllCollections = "collections:all";
    public static string ProductDetail(Guid id) => $"product:{id}";
    public static string RelatedProducts(Guid id) => $"product:{id}:related";
    public static string ProductsPrefix => "products:";
    public static string BundlesPrefix => "bundles:";
    public static string CollectionsPrefix => "collections:";
}
