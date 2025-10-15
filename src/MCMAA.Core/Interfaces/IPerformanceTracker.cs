using System.Diagnostics;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for tracking performance and creating performance contexts
/// </summary>
public interface IPerformanceTracker
{
    /// <summary>
    /// Start tracking a performance operation
    /// </summary>
    IPerformanceContext StartTracking(string operationName, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Track a simple operation with automatic timing
    /// </summary>
    Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Track a simple operation with automatic timing (void return)
    /// </summary>
    Task TrackAsync(string operationName, Func<Task> operation, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Get current performance statistics
    /// </summary>
    Task<PerformanceStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get performance history for an operation
    /// </summary>
    Task<List<PerformanceRecord>> GetOperationHistoryAsync(string operationName, int maxRecords = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear performance history older than specified time
    /// </summary>
    Task CleanupHistoryAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}

/// <summary>
/// Performance tracking context for measuring operations
/// </summary>
public interface IPerformanceContext : IDisposable
{
    /// <summary>
    /// Operation name being tracked
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// When the operation started
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Current elapsed time
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Add metadata to the performance context
    /// </summary>
    void AddMetadata(string key, object value);

    /// <summary>
    /// Add multiple metadata entries
    /// </summary>
    void AddMetadata(Dictionary<string, object> metadata);

    /// <summary>
    /// Mark the operation as failed with error information
    /// </summary>
    void MarkFailed(Exception exception);

    /// <summary>
    /// Mark the operation as failed with error message
    /// </summary>
    void MarkFailed(string errorMessage);

    /// <summary>
    /// Add a checkpoint to track intermediate progress
    /// </summary>
    void AddCheckpoint(string name, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Complete the operation and record the performance data
    /// </summary>
    Task CompleteAsync();
}

/// <summary>
/// Performance statistics summary
/// </summary>
public class PerformanceStatistics
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public Dictionary<string, OperationStatistics> OperationStats { get; set; } = new();
    public List<string> SlowestOperations { get; set; } = new();
    public List<string> MostFrequentErrors { get; set; } = new();
}

/// <summary>
/// Statistics for a specific operation type
/// </summary>
public class OperationStatistics
{
    public string OperationName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public DateTime LastExecution { get; set; }
    public List<string> RecentErrors { get; set; } = new();
}

/// <summary>
/// Individual performance record
/// </summary>
public class PerformanceRecord
{
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<PerformanceCheckpoint> Checkpoints { get; set; } = new();
}

/// <summary>
/// Performance checkpoint for tracking intermediate progress
/// </summary>
public class PerformanceCheckpoint
{
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan ElapsedSinceStart { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
