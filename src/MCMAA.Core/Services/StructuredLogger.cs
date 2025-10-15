using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace MCMAA.Core.Services;

/// <summary>
/// Enhanced structured logger with file output and JSON formatting
/// </summary>
public class StructuredLogger : ILogger
{
    private readonly string _categoryName;
    private readonly StructuredLoggerProvider _provider;

    public StructuredLogger(string categoryName, StructuredLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _provider.ScopeProvider?.Push(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _provider.MinLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Category = _categoryName,
            EventId = eventId.Id,
            EventName = eventId.Name,
            Message = formatter(state, exception),
            Exception = exception?.ToString(),
            Properties = ExtractProperties(state)
        };

        _provider.WriteLog(logEntry);
    }

    private Dictionary<string, object?> ExtractProperties<TState>(TState state)
    {
        var properties = new Dictionary<string, object?>();

        if (state is IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
        {
            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Key != "{OriginalFormat}")
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
        }

        return properties;
    }
}

/// <summary>
/// Provider for structured logger instances
/// </summary>
public class StructuredLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, StructuredLogger> _loggers = new();
    private readonly string _logDirectory;
    private readonly string _logFilePrefix;
    private readonly Timer _flushTimer;
    private readonly ConcurrentQueue<StructuredLogEntry> _logQueue = new();
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

    public LogLevel MinLogLevel { get; set; } = LogLevel.Information;
    public IExternalScopeProvider? ScopeProvider { get; set; }

    public StructuredLoggerProvider(string logDirectory = "logs", string logFilePrefix = "mcmaa")
    {
        _logDirectory = logDirectory;
        _logFilePrefix = logFilePrefix;

        // Ensure log directory exists
        Directory.CreateDirectory(_logDirectory);

        // Setup periodic flush (every 5 seconds)
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new StructuredLogger(name, this));
    }

    public void WriteLog(StructuredLogEntry entry)
    {
        _logQueue.Enqueue(entry);
    }

    private async void FlushLogs(object? state)
    {
        if (_logQueue.IsEmpty)
            return;

        await _writeSemaphore.WaitAsync();
        try
        {
            var entries = new List<StructuredLogEntry>();
            while (_logQueue.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            if (entries.Any())
            {
                await WriteLogEntriesToFile(entries);
            }
        }
        catch (Exception ex)
        {
            // Fallback to console if file writing fails
            Console.WriteLine($"Failed to write logs to file: {ex.Message}");
            foreach (var entry in _logQueue.ToArray())
            {
                Console.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Category}: {entry.Message}");
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private async Task WriteLogEntriesToFile(List<StructuredLogEntry> entries)
    {
        var logFileName = $"{_logFilePrefix}-{DateTime.UtcNow:yyyy-MM-dd}.log";
        var logFilePath = Path.Combine(_logDirectory, logFileName);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        using var writer = new StreamWriter(logFilePath, append: true);
        
        foreach (var entry in entries)
        {
            var json = JsonSerializer.Serialize(entry, jsonOptions);
            await writer.WriteLineAsync(json);
        }

        await writer.FlushAsync();
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushLogs(null);
        _writeSemaphore.Dispose();
    }
}

/// <summary>
/// Structured log entry for JSON serialization
/// </summary>
public class StructuredLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// Extension methods for enhanced logging
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Log with structured data
    /// </summary>
    public static void LogStructured(this ILogger logger, LogLevel level, string message, object? data = null)
    {
        if (data != null)
        {
            var properties = new Dictionary<string, object?>();
            
            foreach (var prop in data.GetType().GetProperties())
            {
                properties[prop.Name] = prop.GetValue(data);
            }

            using (logger.BeginScope(properties))
            {
                logger.Log(level, message);
            }
        }
        else
        {
            logger.Log(level, message);
        }
    }

    /// <summary>
    /// Log performance metrics
    /// </summary>
    public static void LogPerformance(this ILogger logger, string operation, TimeSpan duration, bool success = true, Dictionary<string, object>? metadata = null)
    {
        var data = new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["success"] = success
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        using (logger.BeginScope(data))
        {
            var status = success ? "completed" : "failed";
            logger.LogInformation("Performance: {Operation} {Status} in {Duration}ms", operation, status, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Log scan operation
    /// </summary>
    public static void LogScanOperation(this ILogger logger, string path, TimeSpan duration, int filesScanned, int modsFound, int configsFound)
    {
        var data = new Dictionary<string, object>
        {
            ["scan_path"] = path,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["files_scanned"] = filesScanned,
            ["mods_found"] = modsFound,
            ["configs_found"] = configsFound
        };

        using (logger.BeginScope(data))
        {
            logger.LogInformation("Scan completed: {Path} - {FilesScanned} files, {ModsFound} mods, {ConfigsFound} configs in {Duration}ms", 
                path, filesScanned, modsFound, configsFound, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Log AI analysis operation
    /// </summary>
    public static void LogAnalysisOperation(this ILogger logger, string model, string taskType, TimeSpan duration, int tokensUsed, bool fromCache, bool success)
    {
        var data = new Dictionary<string, object>
        {
            ["model"] = model,
            ["task_type"] = taskType,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["tokens_used"] = tokensUsed,
            ["from_cache"] = fromCache,
            ["success"] = success
        };

        using (logger.BeginScope(data))
        {
            var cacheStatus = fromCache ? "cached" : "fresh";
            var status = success ? "completed" : "failed";
            logger.LogInformation("Analysis {Status}: {Model} {TaskType} ({CacheStatus}) - {TokensUsed} tokens in {Duration}ms", 
                status, model, taskType, cacheStatus, tokensUsed, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Log preprocessing operation
    /// </summary>
    public static void LogPreprocessingOperation(this ILogger logger, string taskType, TimeSpan duration, int originalTokens, int optimizedTokens, double compressionRatio)
    {
        var data = new Dictionary<string, object>
        {
            ["task_type"] = taskType,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["original_tokens"] = originalTokens,
            ["optimized_tokens"] = optimizedTokens,
            ["compression_ratio"] = compressionRatio
        };

        using (logger.BeginScope(data))
        {
            logger.LogInformation("Preprocessing completed: {TaskType} - {OriginalTokens} -> {OptimizedTokens} tokens ({CompressionRatio:P1}) in {Duration}ms", 
                taskType, originalTokens, optimizedTokens, compressionRatio, duration.TotalMilliseconds);
        }
    }
}
