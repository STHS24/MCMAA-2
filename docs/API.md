# MCMAA API Documentation

This document provides comprehensive API documentation for the MCMAA (Minecraft Modpack Config AI Assistant) core interfaces and services.

## Core Interfaces

### IModpackScanner

Responsible for scanning and parsing modpack configuration files.

```csharp
public interface IModpackScanner
{
    Task<ScanResult> ScanAsync(string path, CancellationToken cancellationToken = default);
}
```

**Methods:**
- `ScanAsync(string path, CancellationToken cancellationToken)`: Scans the specified path for modpack configurations

**Returns:** `ScanResult` containing discovered files, mods, configurations, and metadata.

### IAiAssistant

Provides AI-powered analysis capabilities using Ollama.

```csharp
public interface IAiAssistant
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<AiAnalysisResult> AnalyzeAsync(PreprocessedContent content, AnalysisTask task, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> AnalyzeStreamingAsync(PreprocessedContent content, AnalysisTask task, CancellationToken cancellationToken = default);
}
```

**Methods:**
- `IsAvailableAsync()`: Checks if Ollama service is available
- `GetAvailableModelsAsync()`: Retrieves list of available models
- `AnalyzeAsync()`: Performs complete analysis and returns full result
- `AnalyzeStreamingAsync()`: Performs streaming analysis with real-time output

### ICacheService

Provides SHA256-based response caching with intelligent management.

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
```

**Methods:**
- `GetAsync<T>()`: Retrieves cached value by key
- `SetAsync<T>()`: Stores value in cache with optional expiration
- `RemoveAsync()`: Removes specific cache entry
- `ClearAsync()`: Clears all cache entries
- `GetStatisticsAsync()`: Returns cache usage statistics

### ISessionManager

Manages Ollama connection sessions with pooling and health monitoring.

```csharp
public interface ISessionManager
{
    Task<ISession> AcquireSessionAsync(string model, CancellationToken cancellationToken = default);
    Task ReleaseSessionAsync(ISession session, CancellationToken cancellationToken = default);
    Task<SessionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
```

**Methods:**
- `AcquireSessionAsync()`: Acquires a session for the specified model
- `ReleaseSessionAsync()`: Releases a session back to the pool
- `GetStatisticsAsync()`: Returns session pool statistics

### IContentPreprocessor

Optimizes content for AI processing with token management and prioritization.

```csharp
public interface IContentPreprocessor
{
    Task<PreprocessedContent> PreprocessAsync(ScanResult scanResult, AnalysisTask task, CancellationToken cancellationToken = default);
    int EstimateTokens(string content);
    Task<ContentSummary> SummarizeAsync(string content, int maxTokens, CancellationToken cancellationToken = default);
}
```

**Methods:**
- `PreprocessAsync()`: Preprocesses scan results for AI analysis
- `EstimateTokens()`: Estimates token count for content
- `SummarizeAsync()`: Creates content summary within token limits

### IStreamingHandler

Handles real-time streaming output with progress tracking.

```csharp
public interface IStreamingHandler
{
    Task ProcessStreamAsync(IAsyncEnumerable<string> chunks, StreamingOptions options, CancellationToken cancellationToken = default);
    Task<StreamingStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
```

**Methods:**
- `ProcessStreamAsync()`: Processes streaming chunks with progress display
- `GetStatisticsAsync()`: Returns streaming performance statistics

### IMetricsCollector

Collects and manages performance metrics and analytics.

```csharp
public interface IMetricsCollector
{
    Task RecordScanMetricAsync(ScanMetric metric, CancellationToken cancellationToken = default);
    Task RecordAnalysisMetricAsync(AnalysisMetric metric, CancellationToken cancellationToken = default);
    Task RecordPreprocessingMetricAsync(PreprocessingMetric metric, CancellationToken cancellationToken = default);
    Task RecordStreamingMetricAsync(StreamingMetric metric, CancellationToken cancellationToken = default);
    Task<MetricsReport> GetMetricsReportAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);
    Task<string> ExportMetricsAsync(MetricsExportFormat format = MetricsExportFormat.Json, CancellationToken cancellationToken = default);
}
```

**Methods:**
- `RecordScanMetricAsync()`: Records scan operation metrics
- `RecordAnalysisMetricAsync()`: Records AI analysis metrics
- `RecordPreprocessingMetricAsync()`: Records preprocessing metrics
- `RecordStreamingMetricAsync()`: Records streaming metrics
- `GetMetricsReportAsync()`: Generates comprehensive metrics report
- `ExportMetricsAsync()`: Exports metrics in specified format

### IPerformanceTracker

Tracks operation performance with detailed timing and context.

```csharp
public interface IPerformanceTracker
{
    IPerformanceContext StartTracking(string operationName, Dictionary<string, object>? metadata = null);
    Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation, Dictionary<string, object>? metadata = null);
    Task<PerformanceStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
```

**Methods:**
- `StartTracking()`: Starts manual performance tracking
- `TrackAsync<T>()`: Automatically tracks operation performance
- `GetStatisticsAsync()`: Returns performance statistics

## Data Models

### ScanResult

Contains the results of a modpack scan operation.

```csharp
public class ScanResult
{
    public string ScanPath { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<ModInfo> Mods { get; set; } = new();
    public List<ConfigFile> ConfigFiles { get; set; } = new();
    public List<ResourcePack> ResourcePacks { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public ScanStatistics Statistics { get; set; } = new();
}
```

### AiAnalysisResult

Contains the results of AI analysis.

```csharp
public class AiAnalysisResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public AnalysisTaskType TaskType { get; set; }
    public TimeSpan Duration { get; set; }
    public int TokensUsed { get; set; }
    public bool FromCache { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### PreprocessedContent

Represents content optimized for AI processing.

```csharp
public class PreprocessedContent
{
    public string Content { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
    public double CompressionRatio { get; set; }
    public PrioritizedSections Sections { get; set; } = new();
    public List<string> OptimizationSteps { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### AnalysisTask

Defines an analysis task configuration.

```csharp
public class AnalysisTask
{
    public AnalysisTaskType Type { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public Dictionary<string, object> Parameters { get; set; } = new();
}
```

## Enumerations

### AnalysisTaskType

Defines the types of analysis tasks available.

```csharp
public enum AnalysisTaskType
{
    Quick,      // Fast overview analysis
    Full,       // Comprehensive analysis
    Conflicts,  // Conflict detection focus
    Performance,// Performance optimization focus
    Summary     // Summary generation
}
```

### MetricsExportFormat

Defines supported metrics export formats.

```csharp
public enum MetricsExportFormat
{
    Json,   // JSON format
    Csv,    // CSV format
    Xml     // XML format
}
```

## Usage Examples

### Basic Scanning

```csharp
var scanner = serviceProvider.GetRequiredService<IModpackScanner>();
var result = await scanner.ScanAsync("/path/to/modpack");

Console.WriteLine($"Found {result.Mods.Count} mods and {result.ConfigFiles.Count} config files");
```

### AI Analysis with Caching

```csharp
var aiAssistant = serviceProvider.GetRequiredService<IAiAssistant>();
var cacheService = serviceProvider.GetRequiredService<ICacheService>();
var preprocessor = serviceProvider.GetRequiredService<IContentPreprocessor>();

// Check cache first
var cacheKey = GenerateCacheKey(scanResult, task);
var cachedResult = await cacheService.GetAsync<AiAnalysisResult>(cacheKey);

if (cachedResult == null)
{
    // Preprocess content
    var preprocessed = await preprocessor.PreprocessAsync(scanResult, task);
    
    // Perform analysis
    var result = await aiAssistant.AnalyzeAsync(preprocessed, task);
    
    // Cache result
    await cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
    
    return result;
}

return cachedResult;
```

### Performance Tracking

```csharp
var performanceTracker = serviceProvider.GetRequiredService<IPerformanceTracker>();

// Manual tracking
using var context = performanceTracker.StartTracking("MyOperation");
context.AddMetadata("param1", "value1");
context.AddCheckpoint("Phase1");

// ... do work ...

context.AddCheckpoint("Phase2");
await context.CompleteAsync();

// Automatic tracking
var result = await performanceTracker.TrackAsync("MyOperation", async () =>
{
    // ... operation code ...
    return someResult;
});
```

### Metrics Collection

```csharp
var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();

// Record custom metric
await metricsCollector.RecordScanMetricAsync(new ScanMetric
{
    ScanPath = "/path/to/modpack",
    Duration = TimeSpan.FromMilliseconds(250),
    FilesScanned = 100,
    ModsFound = 25
});

// Generate report
var report = await metricsCollector.GetMetricsReportAsync();
Console.WriteLine($"Total operations: {report.TotalScans + report.TotalAnalyses}");

// Export metrics
var jsonData = await metricsCollector.ExportMetricsAsync(MetricsExportFormat.Json);
await File.WriteAllTextAsync("metrics.json", jsonData);
```
