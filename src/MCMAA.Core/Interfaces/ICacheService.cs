namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for caching functionality
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key
    /// </summary>
    /// <typeparam name="T">Type of cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a value in the cache
    /// </summary>
    /// <typeparam name="T">Type of value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiry">Optional expiry time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a value from the cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached values
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a cache key from content and parameters
    /// </summary>
    /// <param name="content">Content to hash</param>
    /// <param name="model">Model used</param>
    /// <param name="temperature">Temperature parameter</param>
    /// <param name="additionalParams">Additional parameters</param>
    /// <returns>Generated cache key</returns>
    string GenerateKey(string content, string model, double temperature, params string[] additionalParams);

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    /// <returns>Cache statistics</returns>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Performs cache cleanup (removes expired entries)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache statistics information
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
    public int TotalRequests { get; set; }
    public DateTime LastCleanup { get; set; }
}
