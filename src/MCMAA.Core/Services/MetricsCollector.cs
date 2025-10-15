using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using MCMAA.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MCMAA.Core.Services;

/// <summary>
/// Collects and manages performance metrics
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly CacheConfiguration _cacheConfig;
    
    private readonly ConcurrentQueue<ScanMetric> _scanMetrics = new();
    private readonly ConcurrentQueue<AnalysisMetric> _analysisMetrics = new();
    private readonly ConcurrentQueue<PreprocessingMetric> _preprocessingMetrics = new();
    private readonly ConcurrentQueue<StreamingMetric> _streamingMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CustomMetric>> _customMetrics = new();
    
    private readonly object _lockObject = new();
    private readonly Timer _cleanupTimer;

    public MetricsCollector(
        ILogger<MetricsCollector> logger,
        IOptions<CacheConfiguration> cacheConfig)
    {
        _logger = logger;
        _cacheConfig = cacheConfig.Value;
        
        // Setup periodic cleanup (every hour)
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public Task RecordScanMetricAsync(ScanMetric metric, CancellationToken cancellationToken = default)
    {
        _scanMetrics.Enqueue(metric);
        _logger.LogDebug("Recorded scan metric: {Path} in {Duration}ms", 
            metric.ScanPath, metric.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task RecordAnalysisMetricAsync(AnalysisMetric metric, CancellationToken cancellationToken = default)
    {
        _analysisMetrics.Enqueue(metric);
        _logger.LogDebug("Recorded analysis metric: {Model} {TaskType} in {Duration}ms", 
            metric.Model, metric.TaskType, metric.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task RecordPreprocessingMetricAsync(PreprocessingMetric metric, CancellationToken cancellationToken = default)
    {
        _preprocessingMetrics.Enqueue(metric);
        _logger.LogDebug("Recorded preprocessing metric: {TaskType} {OriginalTokens}->{OptimizedTokens} tokens in {Duration}ms", 
            metric.TaskType, metric.OriginalTokens, metric.OptimizedTokens, metric.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task RecordStreamingMetricAsync(StreamingMetric metric, CancellationToken cancellationToken = default)
    {
        _streamingMetrics.Enqueue(metric);
        _logger.LogDebug("Recorded streaming metric: {Chunks} chunks, {Bytes} bytes in {Duration}ms", 
            metric.ChunksProcessed, metric.BytesProcessed, metric.Duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task RecordCustomMetricAsync(string name, double value, Dictionary<string, object>? tags = null, CancellationToken cancellationToken = default)
    {
        var metric = new CustomMetric
        {
            Name = name,
            Value = value,
            Tags = tags ?? new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow
        };

        var queue = _customMetrics.GetOrAdd(name, _ => new ConcurrentQueue<CustomMetric>());
        queue.Enqueue(metric);
        
        _logger.LogDebug("Recorded custom metric: {Name} = {Value}", name, value);
        return Task.CompletedTask;
    }

    public Task<MetricsReport> GetMetricsReportAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var start = startTime ?? DateTime.UtcNow.AddHours(-24);
        var end = endTime ?? DateTime.UtcNow;

        var report = new MetricsReport
        {
            StartTime = start,
            EndTime = end,
            ReportPeriod = end - start
        };

        // Filter metrics by time range
        var scanMetrics = _scanMetrics.Where(m => m.Timestamp >= start && m.Timestamp <= end).ToList();
        var analysisMetrics = _analysisMetrics.Where(m => m.Timestamp >= start && m.Timestamp <= end).ToList();
        var preprocessingMetrics = _preprocessingMetrics.Where(m => m.Timestamp >= start && m.Timestamp <= end).ToList();
        var streamingMetrics = _streamingMetrics.Where(m => m.Timestamp >= start && m.Timestamp <= end).ToList();

        // Calculate scan metrics
        if (scanMetrics.Any())
        {
            report.TotalScans = scanMetrics.Count;
            report.AverageScanDuration = TimeSpan.FromMilliseconds(scanMetrics.Average(m => m.Duration.TotalMilliseconds));
            report.TotalFilesScanned = scanMetrics.Sum(m => m.FilesScanned);
            report.TotalBytesScanned = scanMetrics.Sum(m => m.TotalSizeBytes);
            report.TotalErrors = scanMetrics.Sum(m => m.ErrorCount);
            report.TotalWarnings = scanMetrics.Sum(m => m.WarningCount);
        }

        // Calculate analysis metrics
        if (analysisMetrics.Any())
        {
            report.TotalAnalyses = analysisMetrics.Count;
            report.AverageAnalysisDuration = TimeSpan.FromMilliseconds(analysisMetrics.Average(m => m.Duration.TotalMilliseconds));
            report.TotalTokensUsed = analysisMetrics.Sum(m => m.TokensUsed);
            report.CacheHitRate = analysisMetrics.Count(m => m.FromCache) / (double)analysisMetrics.Count;
            report.StreamingUsageRate = analysisMetrics.Count(m => m.StreamingUsed) / (double)analysisMetrics.Count;
            
            report.ModelUsage = analysisMetrics
                .GroupBy(m => m.Model)
                .ToDictionary(g => g.Key, g => g.Count());
            
            report.TaskTypeUsage = analysisMetrics
                .GroupBy(m => m.TaskType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // Calculate preprocessing metrics
        if (preprocessingMetrics.Any())
        {
            report.TotalPreprocessingOperations = preprocessingMetrics.Count;
            report.AveragePreprocessingDuration = TimeSpan.FromMilliseconds(preprocessingMetrics.Average(m => m.Duration.TotalMilliseconds));
            report.AverageCompressionRatio = preprocessingMetrics.Average(m => m.CompressionRatio);
            report.TotalTokensOptimized = preprocessingMetrics.Sum(m => m.OriginalTokens - m.OptimizedTokens);
        }

        // Calculate streaming metrics
        if (streamingMetrics.Any())
        {
            report.TotalStreamingOperations = streamingMetrics.Count;
            report.AverageStreamingDuration = TimeSpan.FromMilliseconds(streamingMetrics.Average(m => m.Duration.TotalMilliseconds));
            report.TotalBytesStreamed = streamingMetrics.Sum(m => m.BytesProcessed);
            report.AverageStreamingRate = streamingMetrics.Average(m => m.ProcessingRate);
            report.TotalStreamingErrors = streamingMetrics.Sum(m => m.ErrorCount);
        }

        // Add system metrics
        report.SystemMetrics["memory_usage_mb"] = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        report.SystemMetrics["gc_collections_gen0"] = GC.CollectionCount(0);
        report.SystemMetrics["gc_collections_gen1"] = GC.CollectionCount(1);
        report.SystemMetrics["gc_collections_gen2"] = GC.CollectionCount(2);
        report.SystemMetrics["thread_count"] = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

        // Get top errors
        var allErrors = new List<string>();
        allErrors.AddRange(analysisMetrics.Where(m => !m.Success && !string.IsNullOrEmpty(m.ErrorMessage)).Select(m => m.ErrorMessage!));
        
        report.TopErrors = allErrors
            .GroupBy(e => e)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key} ({g.Count()} times)")
            .ToList();

        _logger.LogInformation("Generated metrics report for period {Start} to {End}: {Scans} scans, {Analyses} analyses", 
            start, end, report.TotalScans, report.TotalAnalyses);

        return Task.FromResult(report);
    }

    public async Task<string> ExportMetricsAsync(MetricsExportFormat format = MetricsExportFormat.Json, CancellationToken cancellationToken = default)
    {
        var report = await GetMetricsReportAsync(cancellationToken: cancellationToken);

        return format switch
        {
            MetricsExportFormat.Json => JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            MetricsExportFormat.Csv => ExportToCsv(report),
            MetricsExportFormat.Xml => ExportToXml(report),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    public Task CleanupMetricsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        
        CleanupQueue(_scanMetrics, m => m.Timestamp, cutoffTime);
        CleanupQueue(_analysisMetrics, m => m.Timestamp, cutoffTime);
        CleanupQueue(_preprocessingMetrics, m => m.Timestamp, cutoffTime);
        CleanupQueue(_streamingMetrics, m => m.Timestamp, cutoffTime);

        foreach (var customQueue in _customMetrics.Values)
        {
            CleanupQueue(customQueue, m => m.Timestamp, cutoffTime);
        }

        _logger.LogDebug("Cleaned up metrics older than {MaxAge}", maxAge);
        return Task.CompletedTask;
    }

    private void CleanupQueue<T>(ConcurrentQueue<T> queue, Func<T, DateTime> timestampSelector, DateTime cutoffTime)
    {
        var itemsToKeep = new List<T>();
        
        while (queue.TryDequeue(out var item))
        {
            if (timestampSelector(item) >= cutoffTime)
            {
                itemsToKeep.Add(item);
            }
        }

        foreach (var item in itemsToKeep)
        {
            queue.Enqueue(item);
        }
    }

    private string ExportToCsv(MetricsReport report)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Metric,Value,Unit");
        csv.AppendLine($"Total Scans,{report.TotalScans},count");
        csv.AppendLine($"Average Scan Duration,{report.AverageScanDuration.TotalMilliseconds},ms");
        csv.AppendLine($"Total Files Scanned,{report.TotalFilesScanned},count");
        csv.AppendLine($"Total Analyses,{report.TotalAnalyses},count");
        csv.AppendLine($"Average Analysis Duration,{report.AverageAnalysisDuration.TotalMilliseconds},ms");
        csv.AppendLine($"Cache Hit Rate,{report.CacheHitRate:P2},percentage");
        csv.AppendLine($"Total Tokens Used,{report.TotalTokensUsed},count");
        return csv.ToString();
    }

    private string ExportToXml(MetricsReport report)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<MetricsReport>
    <GeneratedAt>{report.GeneratedAt:yyyy-MM-ddTHH:mm:ss}</GeneratedAt>
    <Period>
        <Start>{report.StartTime:yyyy-MM-ddTHH:mm:ss}</Start>
        <End>{report.EndTime:yyyy-MM-ddTHH:mm:ss}</End>
    </Period>
    <Scans>
        <Total>{report.TotalScans}</Total>
        <AverageDuration>{report.AverageScanDuration.TotalMilliseconds}</AverageDuration>
        <FilesScanned>{report.TotalFilesScanned}</FilesScanned>
    </Scans>
    <Analyses>
        <Total>{report.TotalAnalyses}</Total>
        <AverageDuration>{report.AverageAnalysisDuration.TotalMilliseconds}</AverageDuration>
        <TokensUsed>{report.TotalTokensUsed}</TokensUsed>
        <CacheHitRate>{report.CacheHitRate:P2}</CacheHitRate>
    </Analyses>
</MetricsReport>";
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            var maxAge = TimeSpan.FromDays(7); // Keep metrics for 7 days
            CleanupMetricsAsync(maxAge).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics cleanup");
        }
    }
}

/// <summary>
/// Custom metric entry
/// </summary>
internal class CustomMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, object> Tags { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
