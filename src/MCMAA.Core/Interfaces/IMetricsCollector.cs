using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for collecting and tracking performance metrics
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Record a scan operation metric
    /// </summary>
    Task RecordScanMetricAsync(ScanMetric metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record an AI analysis metric
    /// </summary>
    Task RecordAnalysisMetricAsync(AnalysisMetric metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a preprocessing metric
    /// </summary>
    Task RecordPreprocessingMetricAsync(PreprocessingMetric metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a streaming metric
    /// </summary>
    Task RecordStreamingMetricAsync(StreamingMetric metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a custom metric
    /// </summary>
    Task RecordCustomMetricAsync(string name, double value, Dictionary<string, object>? tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregated metrics for a time period
    /// </summary>
    Task<MetricsReport> GetMetricsReportAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export metrics to JSON format
    /// </summary>
    Task<string> ExportMetricsAsync(MetricsExportFormat format = MetricsExportFormat.Json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear metrics older than specified time
    /// </summary>
    Task CleanupMetricsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}

/// <summary>
/// Scan operation metrics
/// </summary>
public class ScanMetric
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ScanPath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int FilesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public int ModsFound { get; set; }
    public int ConfigFilesFound { get; set; }
    public int ResourcePacksFound { get; set; }
    public long TotalSizeBytes { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// AI analysis metrics
/// </summary>
public class AnalysisMetric
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public AnalysisTaskType TaskType { get; set; }
    public TimeSpan Duration { get; set; }
    public int TokensUsed { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool FromCache { get; set; }
    public bool StreamingUsed { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double Temperature { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Content preprocessing metrics
/// </summary>
public class PreprocessingMetric
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AnalysisTaskType TaskType { get; set; }
    public TimeSpan Duration { get; set; }
    public int OriginalTokens { get; set; }
    public int OptimizedTokens { get; set; }
    public double CompressionRatio { get; set; }
    public int HighPrioritySections { get; set; }
    public int MediumPrioritySections { get; set; }
    public int LowPrioritySections { get; set; }
    public List<string> OptimizationSteps { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Streaming operation metrics
/// </summary>
public class StreamingMetric
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public int ChunksProcessed { get; set; }
    public long BytesProcessed { get; set; }
    public int ErrorCount { get; set; }
    public int RetryCount { get; set; }
    public double AverageChunkSize { get; set; }
    public double ProcessingRate { get; set; } // Bytes per second
    public bool Success { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Aggregated metrics report
/// </summary>
public class MetricsReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan ReportPeriod { get; set; }

    // Scan metrics
    public int TotalScans { get; set; }
    public TimeSpan AverageScanDuration { get; set; }
    public long TotalFilesScanned { get; set; }
    public long TotalBytesScanned { get; set; }
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }

    // Analysis metrics
    public int TotalAnalyses { get; set; }
    public TimeSpan AverageAnalysisDuration { get; set; }
    public long TotalTokensUsed { get; set; }
    public double CacheHitRate { get; set; }
    public double StreamingUsageRate { get; set; }
    public Dictionary<string, int> ModelUsage { get; set; } = new();
    public Dictionary<AnalysisTaskType, int> TaskTypeUsage { get; set; } = new();

    // Preprocessing metrics
    public int TotalPreprocessingOperations { get; set; }
    public TimeSpan AveragePreprocessingDuration { get; set; }
    public double AverageCompressionRatio { get; set; }
    public long TotalTokensOptimized { get; set; }

    // Streaming metrics
    public int TotalStreamingOperations { get; set; }
    public TimeSpan AverageStreamingDuration { get; set; }
    public long TotalBytesStreamed { get; set; }
    public double AverageStreamingRate { get; set; }
    public int TotalStreamingErrors { get; set; }

    // System metrics
    public Dictionary<string, object> SystemMetrics { get; set; } = new();
    public List<string> TopErrors { get; set; } = new();
    public Dictionary<string, double> PerformanceTrends { get; set; } = new();
}

/// <summary>
/// Metrics export formats
/// </summary>
public enum MetricsExportFormat
{
    Json,
    Csv,
    Xml
}
