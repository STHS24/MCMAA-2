namespace MCMAA.Core.Configuration;

/// <summary>
/// Configuration for response caching system
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Enable caching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum cache size in MB
    /// </summary>
    public int MaxSizeMb { get; set; } = 500;

    /// <summary>
    /// Cache expiry in days
    /// </summary>
    public int ExpiryDays { get; set; } = 7;

    /// <summary>
    /// Hash algorithm for cache keys
    /// </summary>
    public string HashAlgorithm { get; set; } = "sha256";

    /// <summary>
    /// Cache directory path
    /// </summary>
    public string CacheDirectory { get; set; } = "cache";

    /// <summary>
    /// Enable cache statistics tracking
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Cache cleanup interval in hours
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;
}
