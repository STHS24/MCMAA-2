using MCMAA.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MCMAA.Core.Services;

/// <summary>
/// Tracks performance metrics and provides performance contexts
/// </summary>
public class PerformanceTracker : IPerformanceTracker
{
    private readonly ILogger<PerformanceTracker> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PerformanceRecord>> _operationHistory = new();
    private readonly object _lockObject = new();
    private readonly Timer _cleanupTimer;

    public PerformanceTracker(ILogger<PerformanceTracker> logger)
    {
        _logger = logger;
        
        // Setup periodic cleanup (every 30 minutes)
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    public IPerformanceContext StartTracking(string operationName, Dictionary<string, object>? metadata = null)
    {
        return new PerformanceContext(operationName, this, _logger, metadata);
    }

    public async Task<T> TrackAsync<T>(string operationName, Func<Task<T>> operation, Dictionary<string, object>? metadata = null)
    {
        using var context = StartTracking(operationName, metadata);
        
        try
        {
            var result = await operation();
            await context.CompleteAsync();
            return result;
        }
        catch (Exception ex)
        {
            context.MarkFailed(ex);
            await context.CompleteAsync();
            throw;
        }
    }

    public async Task TrackAsync(string operationName, Func<Task> operation, Dictionary<string, object>? metadata = null)
    {
        using var context = StartTracking(operationName, metadata);
        
        try
        {
            await operation();
            await context.CompleteAsync();
        }
        catch (Exception ex)
        {
            context.MarkFailed(ex);
            await context.CompleteAsync();
            throw;
        }
    }

    public Task<PerformanceStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var allRecords = new List<PerformanceRecord>();
        
        foreach (var queue in _operationHistory.Values)
        {
            allRecords.AddRange(queue.ToArray());
        }

        var stats = new PerformanceStatistics();
        
        if (allRecords.Any())
        {
            stats.TotalOperations = allRecords.Count;
            stats.SuccessfulOperations = allRecords.Count(r => r.Success);
            stats.FailedOperations = allRecords.Count(r => !r.Success);
            stats.SuccessRate = (double)stats.SuccessfulOperations / stats.TotalOperations;
            stats.TotalDuration = TimeSpan.FromMilliseconds(allRecords.Sum(r => r.Duration.TotalMilliseconds));
            stats.AverageDuration = TimeSpan.FromMilliseconds(allRecords.Average(r => r.Duration.TotalMilliseconds));
            stats.MinDuration = allRecords.Min(r => r.Duration);
            stats.MaxDuration = allRecords.Max(r => r.Duration);

            // Operation-specific statistics
            stats.OperationStats = allRecords
                .GroupBy(r => r.OperationName)
                .ToDictionary(g => g.Key, g => new OperationStatistics
                {
                    OperationName = g.Key,
                    TotalCount = g.Count(),
                    SuccessCount = g.Count(r => r.Success),
                    FailureCount = g.Count(r => !r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count(),
                    TotalDuration = TimeSpan.FromMilliseconds(g.Sum(r => r.Duration.TotalMilliseconds)),
                    AverageDuration = TimeSpan.FromMilliseconds(g.Average(r => r.Duration.TotalMilliseconds)),
                    MinDuration = g.Min(r => r.Duration),
                    MaxDuration = g.Max(r => r.Duration),
                    LastExecution = g.Max(r => r.EndTime),
                    RecentErrors = g.Where(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage))
                                   .OrderByDescending(r => r.EndTime)
                                   .Take(5)
                                   .Select(r => r.ErrorMessage!)
                                   .ToList()
                });

            // Slowest operations
            stats.SlowestOperations = allRecords
                .OrderByDescending(r => r.Duration)
                .Take(10)
                .Select(r => $"{r.OperationName}: {r.Duration.TotalMilliseconds:F0}ms")
                .ToList();

            // Most frequent errors
            stats.MostFrequentErrors = allRecords
                .Where(r => !r.Success && !string.IsNullOrEmpty(r.ErrorMessage))
                .GroupBy(r => r.ErrorMessage!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key} ({g.Count()} times)")
                .ToList();
        }

        return Task.FromResult(stats);
    }

    public Task<List<PerformanceRecord>> GetOperationHistoryAsync(string operationName, int maxRecords = 100, CancellationToken cancellationToken = default)
    {
        if (_operationHistory.TryGetValue(operationName, out var queue))
        {
            var records = queue.ToArray()
                             .OrderByDescending(r => r.EndTime)
                             .Take(maxRecords)
                             .ToList();
            return Task.FromResult(records);
        }

        return Task.FromResult(new List<PerformanceRecord>());
    }

    public Task CleanupHistoryAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var totalRemoved = 0;

        foreach (var kvp in _operationHistory)
        {
            var queue = kvp.Value;
            var itemsToKeep = new List<PerformanceRecord>();
            var removedCount = 0;

            while (queue.TryDequeue(out var record))
            {
                if (record.EndTime >= cutoffTime)
                {
                    itemsToKeep.Add(record);
                }
                else
                {
                    removedCount++;
                }
            }

            foreach (var record in itemsToKeep)
            {
                queue.Enqueue(record);
            }

            totalRemoved += removedCount;
        }

        _logger.LogDebug("Cleaned up {Count} performance records older than {MaxAge}", totalRemoved, maxAge);
        return Task.CompletedTask;
    }

    internal void RecordPerformance(PerformanceRecord record)
    {
        var queue = _operationHistory.GetOrAdd(record.OperationName, _ => new ConcurrentQueue<PerformanceRecord>());
        queue.Enqueue(record);

        // Keep only the last 1000 records per operation to prevent memory issues
        while (queue.Count > 1000)
        {
            queue.TryDequeue(out _);
        }

        _logger.LogDebug("Recorded performance for {Operation}: {Duration}ms (Success: {Success})", 
            record.OperationName, record.Duration.TotalMilliseconds, record.Success);
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            var maxAge = TimeSpan.FromHours(24); // Keep performance data for 24 hours
            CleanupHistoryAsync(maxAge).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during performance history cleanup");
        }
    }
}

/// <summary>
/// Performance tracking context implementation
/// </summary>
internal class PerformanceContext : IPerformanceContext
{
    private readonly PerformanceTracker _tracker;
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, object> _metadata;
    private readonly List<PerformanceCheckpoint> _checkpoints;
    
    private bool _completed = false;
    private bool _failed = false;
    private string? _errorMessage;

    public string OperationName { get; }
    public DateTime StartTime { get; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public PerformanceContext(string operationName, PerformanceTracker tracker, ILogger logger, Dictionary<string, object>? metadata = null)
    {
        OperationName = operationName;
        _tracker = tracker;
        _logger = logger;
        _metadata = metadata ?? new Dictionary<string, object>();
        _checkpoints = new List<PerformanceCheckpoint>();
        
        StartTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug("Started tracking performance for operation: {Operation}", operationName);
    }

    public void AddMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    public void AddMetadata(Dictionary<string, object> metadata)
    {
        foreach (var kvp in metadata)
        {
            _metadata[kvp.Key] = kvp.Value;
        }
    }

    public void MarkFailed(Exception exception)
    {
        _failed = true;
        _errorMessage = exception.Message;
        _metadata["exception_type"] = exception.GetType().Name;
        _metadata["stack_trace"] = exception.StackTrace ?? string.Empty;
    }

    public void MarkFailed(string errorMessage)
    {
        _failed = true;
        _errorMessage = errorMessage;
    }

    public void AddCheckpoint(string name, Dictionary<string, object>? metadata = null)
    {
        var checkpoint = new PerformanceCheckpoint
        {
            Name = name,
            Timestamp = DateTime.UtcNow,
            ElapsedSinceStart = _stopwatch.Elapsed,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
        
        _checkpoints.Add(checkpoint);
        _logger.LogDebug("Added checkpoint '{Checkpoint}' to operation {Operation} at {Elapsed}ms", 
            name, OperationName, checkpoint.ElapsedSinceStart.TotalMilliseconds);
    }

    public Task CompleteAsync()
    {
        if (_completed) return Task.CompletedTask;

        _stopwatch.Stop();
        _completed = true;

        var record = new PerformanceRecord
        {
            OperationName = OperationName,
            StartTime = StartTime,
            EndTime = DateTime.UtcNow,
            Duration = _stopwatch.Elapsed,
            Success = !_failed,
            ErrorMessage = _errorMessage,
            Metadata = new Dictionary<string, object>(_metadata),
            Checkpoints = new List<PerformanceCheckpoint>(_checkpoints)
        };

        _tracker.RecordPerformance(record);

        var status = _failed ? "failed" : "completed";
        _logger.LogDebug("Performance tracking {Status} for operation {Operation}: {Duration}ms", 
            status, OperationName, _stopwatch.Elapsed.TotalMilliseconds);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            CompleteAsync().Wait();
        }
    }
}
