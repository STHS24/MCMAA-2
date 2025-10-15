using MCMAA.Core.Configuration;
using MCMAA.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MCMAA.Core.Services;

/// <summary>
/// File-based cache service with SHA256 keys and LRU eviction
/// </summary>
public class FileCacheService : ICacheService
{
    private readonly ILogger<FileCacheService> _logger;
    private readonly CacheConfiguration _config;
    private readonly string _cacheDirectory;
    private readonly object _lockObject = new();
    private CacheStatistics _statistics = new();

    public FileCacheService(ILogger<FileCacheService> logger, IOptions<CacheConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        _cacheDirectory = Path.GetFullPath(_config.CacheDirectory);
        
        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
        
        _logger.LogDebug("Cache service initialized with directory: {CacheDirectory}", _cacheDirectory);
    }

    public string GenerateKey(string content, string model, double temperature, params string[] additionalParams)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(content);
        keyBuilder.Append("|");
        keyBuilder.Append(model);
        keyBuilder.Append("|");
        keyBuilder.Append(temperature.ToString("F2"));
        
        foreach (var param in additionalParams)
        {
            keyBuilder.Append("|");
            keyBuilder.Append(param);
        }

        var keyBytes = Encoding.UTF8.GetBytes(keyBuilder.ToString());
        var hashBytes = SHA256.HashData(keyBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (!_config.Enabled)
            return null;

        lock (_lockObject)
        {
            _statistics.TotalRequests = _statistics.HitCount + _statistics.MissCount + 1;
        }

        try
        {
            var filePath = GetCacheFilePath(key);
            if (!File.Exists(filePath))
            {
                lock (_lockObject)
                {
                    _statistics.MissCount++;
                }
                return null;
            }

            var cacheEntry = await ReadCacheEntryAsync(filePath, cancellationToken);
            if (cacheEntry == null || IsExpired(cacheEntry))
            {
                // Remove expired entry
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete expired cache file: {FilePath}", filePath);
                }

                lock (_lockObject)
                {
                    _statistics.MissCount++;
                }
                return null;
            }

            // Update access time for LRU
            cacheEntry.LastAccessed = DateTime.UtcNow;
            await WriteCacheEntryAsync(filePath, cacheEntry, cancellationToken);

            lock (_lockObject)
            {
                _statistics.HitCount++;
            }

            return JsonSerializer.Deserialize<T>(cacheEntry.Data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from cache for key: {Key}", key);
            lock (_lockObject)
            {
                _statistics.MissCount++;
            }
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!_config.Enabled || value == null)
            return;

        try
        {
            var filePath = GetCacheFilePath(key);
            var expiryTime = expiry.HasValue 
                ? DateTime.UtcNow.Add(expiry.Value)
                : DateTime.UtcNow.AddDays(_config.ExpiryDays);

            var cacheEntry = new CacheEntry
            {
                Key = key,
                Data = JsonSerializer.Serialize(value),
                Created = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                Expires = expiryTime
            };

            await WriteCacheEntryAsync(filePath, cacheEntry, cancellationToken);
            
            // Check cache size and cleanup if needed
            _ = Task.Run(async () => await CheckCacheSizeAsync(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing to cache for key: {Key}", key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetCacheFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cache entry for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var files = Directory.GetFiles(_cacheDirectory, "*.cache");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {File}", file);
                    }
                }
            }

            lock (_lockObject)
            {
                _statistics = new CacheStatistics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }

        return Task.CompletedTask;
    }

    public Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_lockObject)
        {
            var stats = new CacheStatistics
            {
                HitCount = _statistics.HitCount,
                MissCount = _statistics.MissCount,
                LastCleanup = _statistics.LastCleanup
            };

            // Calculate total entries and size
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    var files = Directory.GetFiles(_cacheDirectory, "*.cache");
                    stats.TotalEntries = files.Length;
                    stats.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating cache statistics");
            }

            return Task.FromResult(stats);
        }
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting cache cleanup");
            
            var files = Directory.GetFiles(_cacheDirectory, "*.cache");
            var expiredFiles = new List<string>();
            var validEntries = new List<(string filePath, DateTime lastAccessed, long size)>();

            // First pass: identify expired files and collect valid entries
            foreach (var file in files)
            {
                try
                {
                    var entry = await ReadCacheEntryAsync(file, cancellationToken);
                    if (entry == null || IsExpired(entry))
                    {
                        expiredFiles.Add(file);
                    }
                    else
                    {
                        var fileInfo = new FileInfo(file);
                        validEntries.Add((file, entry.LastAccessed, fileInfo.Length));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading cache file during cleanup: {File}", file);
                    expiredFiles.Add(file); // Remove corrupted files
                }
            }

            // Remove expired files
            foreach (var file in expiredFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete expired cache file: {File}", file);
                }
            }

            // Check size limit and remove LRU entries if needed
            var totalSize = validEntries.Sum(e => e.size);
            var maxSizeBytes = _config.MaxSizeMb * 1024 * 1024;

            if (totalSize > maxSizeBytes)
            {
                var sortedEntries = validEntries.OrderBy(e => e.lastAccessed).ToList();
                var currentSize = totalSize;

                foreach (var (filePath, _, size) in sortedEntries)
                {
                    if (currentSize <= maxSizeBytes)
                        break;

                    try
                    {
                        File.Delete(filePath);
                        currentSize -= size;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete LRU cache file: {File}", filePath);
                    }
                }
            }

            lock (_lockObject)
            {
                _statistics.LastCleanup = DateTime.UtcNow;
            }

            _logger.LogDebug("Cache cleanup completed. Removed {ExpiredCount} expired files", expiredFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private string GetCacheFilePath(string key)
    {
        return Path.Combine(_cacheDirectory, $"{key}.cache");
    }

    private async Task<CacheEntry?> ReadCacheEntryAsync(string filePath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<CacheEntry>(json);
    }

    private async Task WriteCacheEntryAsync(string filePath, CacheEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private bool IsExpired(CacheEntry entry)
    {
        return DateTime.UtcNow > entry.Expires;
    }

    private async Task CheckCacheSizeAsync()
    {
        try
        {
            var stats = await GetStatisticsAsync();
            var maxSizeBytes = _config.MaxSizeMb * 1024 * 1024;

            if (stats.TotalSizeBytes > maxSizeBytes)
            {
                await CleanupAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking cache size");
        }
    }
}

/// <summary>
/// Cache entry model for file storage
/// </summary>
internal class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime LastAccessed { get; set; }
    public DateTime Expires { get; set; }
}
