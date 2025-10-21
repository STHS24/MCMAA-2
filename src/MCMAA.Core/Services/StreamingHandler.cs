using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MCMAA.Core.Services;

/// <summary>
/// Enhanced streaming handler with progress tracking and error recovery
/// </summary>
public class StreamingHandler : IStreamingHandler
{
    private readonly ILogger<StreamingHandler> _logger;
    private readonly IMetricsCollector _metricsCollector;
    private readonly IPerformanceTracker _performanceTracker;

    public StreamingHandler(
        ILogger<StreamingHandler> logger,
        IMetricsCollector metricsCollector,
        IPerformanceTracker performanceTracker)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _performanceTracker = performanceTracker;
    }

    public async Task ProcessStreamAsync(
        IAsyncEnumerable<string> chunks,
        StreamingOptions options,
        CancellationToken cancellationToken = default)
    {
        using var performanceContext = _performanceTracker.StartTracking("StreamProcessing", new Dictionary<string, object>
        {
            ["show_progress"] = options.ShowProgress,
            ["enable_error_recovery"] = options.EnableErrorRecovery
        });

        var context = new StreamingContext
        {
            StartTime = DateTime.UtcNow
        };

        var progress = new StreamingProgress
        {
            CurrentStatus = "Starting stream processing..."
        };

        var progressCallback = CreateProgressCallback(options);
        var contentBuilder = new StringBuilder();
        var chunkCount = 0;
        var totalBytes = 0L;

        try
        {
            _logger.LogDebug("Starting stream processing with options: ShowProgress={ShowProgress}, WriteToFile={WriteToFile}",
                options.ShowProgress, options.WriteToFile);

            // Setup file writer if needed
            StreamWriter? fileWriter = null;
            if (options.WriteToFile && !string.IsNullOrEmpty(options.OutputPath))
            {
                var directory = Path.GetDirectoryName(options.OutputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                fileWriter = new StreamWriter(options.OutputPath, false, Encoding.UTF8);
            }

            try
            {
                await foreach (var chunk in chunks.WithCancellation(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Process the chunk
                        var processedChunk = await ProcessChunkAsync(chunk, context, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(processedChunk))
                        {
                            contentBuilder.Append(processedChunk);
                            totalBytes += Encoding.UTF8.GetByteCount(processedChunk);
                            chunkCount++;

                            // Write to file if enabled
                            if (fileWriter != null)
                            {
                                await fileWriter.WriteAsync(processedChunk);
                                await fileWriter.FlushAsync();
                            }

                            // Call chunk callback
                            options.OnChunk?.Invoke(processedChunk);

                            // Update progress
                            progress.ChunksProcessed = chunkCount;
                            progress.BytesProcessed = totalBytes;
                            progress.Elapsed = DateTime.UtcNow - context.StartTime;
                            progress.CurrentStatus = $"Processed {chunkCount} chunks ({FormatBytes(totalBytes)})";

                            progressCallback(progress);
                        }
                    }
                    catch (Exception ex) when (options.EnableErrorRecovery)
                    {
                        _logger.LogWarning(ex, "Error processing chunk {ChunkNumber}, attempting recovery", chunkCount);
                        
                        var recovered = await HandleStreamingErrorAsync(ex, context, cancellationToken);
                        if (!recovered)
                        {
                            throw;
                        }
                        
                        context.RetryCount++;
                        context.Errors.Add($"Chunk {chunkCount}: {ex.Message}");
                    }
                }

                // Final progress update
                progress.PercentComplete = 100.0;
                progress.CurrentStatus = $"Completed: {chunkCount} chunks ({FormatBytes(totalBytes)})";
                progressCallback(progress);

                // Record streaming metrics
                var duration = DateTime.UtcNow - context.StartTime;
                var processingRate = totalBytes > 0 ? totalBytes / duration.TotalSeconds : 0;

                var metric = new StreamingMetric
                {
                    Duration = duration,
                    ChunksProcessed = chunkCount,
                    BytesProcessed = totalBytes,
                    ErrorCount = context.Errors.Count,
                    RetryCount = context.RetryCount,
                    AverageChunkSize = chunkCount > 0 ? (double)totalBytes / chunkCount : 0,
                    ProcessingRate = processingRate,
                    Success = true
                };

                await _metricsCollector.RecordStreamingMetricAsync(metric, cancellationToken);

                performanceContext.AddMetadata("chunks_processed", chunkCount);
                performanceContext.AddMetadata("bytes_processed", totalBytes);
                performanceContext.AddMetadata("processing_rate", processingRate);
                performanceContext.AddCheckpoint("StreamingCompleted");

                _logger.LogInformation("Stream processing completed: {ChunkCount} chunks, {TotalBytes} bytes in {Elapsed}",
                    chunkCount, totalBytes, progress.Elapsed);
            }
            finally
            {
                fileWriter?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            performanceContext.MarkFailed("Operation was cancelled");
            _logger.LogInformation("Stream processing cancelled after {ChunkCount} chunks", chunkCount);
            progress.CurrentStatus = "Cancelled";
            progressCallback(progress);

            // Record cancelled streaming metrics
            await RecordFailedStreamingMetric(context, chunkCount, totalBytes, "Cancelled", cancellationToken);
        }
        catch (Exception ex)
        {
            performanceContext.MarkFailed(ex);
            _logger.LogError(ex, "Stream processing failed after {ChunkCount} chunks", chunkCount);
            progress.CurrentStatus = $"Failed: {ex.Message}";
            progressCallback(progress);
            options.OnError?.Invoke(ex);

            // Record failed streaming metrics
            await RecordFailedStreamingMetric(context, chunkCount, totalBytes, ex.Message, cancellationToken);
            throw;
        }
    }

    public Action<StreamingProgress> CreateProgressCallback(StreamingOptions options)
    {
        var lastUpdate = DateTime.MinValue;
        
        return progress =>
        {
            var now = DateTime.UtcNow;
            
            // Throttle progress updates
            if (now - lastUpdate < options.ProgressUpdateInterval)
                return;
            
            lastUpdate = now;

            if (options.ShowProgress)
            {
                // Console progress display
                var progressBar = CreateProgressBar(progress.PercentComplete);
                var status = $"\r{progressBar} {progress.PercentComplete:F1}% - {progress.CurrentStatus}";
                
                Console.Write(status.PadRight(Console.WindowWidth - 1));
            }

            // Call custom progress callback
            options.OnProgress?.Invoke(progress);
        };
    }

    public async Task<bool> HandleStreamingErrorAsync(
        Exception error, 
        StreamingContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(error, "Handling streaming error for session {SessionId}", context.SessionId);

        // Determine if error is recoverable
        var isRecoverable = error switch
        {
            TimeoutException => true,
            HttpRequestException => true,
            TaskCanceledException when !cancellationToken.IsCancellationRequested => true,
            _ => false
        };

        if (!isRecoverable || context.RetryCount >= 3)
        {
            _logger.LogError("Error is not recoverable or max retries exceeded");
            return false;
        }

        // Exponential backoff
        var delay = TimeSpan.FromMilliseconds(Math.Pow(2, context.RetryCount) * 1000);
        _logger.LogDebug("Waiting {Delay}ms before retry", delay.TotalMilliseconds);
        
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public Task<string> ProcessChunkAsync(
        string chunk,
        StreamingContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(chunk))
            return Task.FromResult(string.Empty);

        // Basic chunk processing - can be extended for more sophisticated handling
        var processedChunk = chunk;

        // Remove any control characters that might interfere with display
        processedChunk = System.Text.RegularExpressions.Regex.Replace(processedChunk, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // Ensure proper line endings
        processedChunk = processedChunk.Replace("\r\n", "\n").Replace("\r", "\n");

        return Task.FromResult(processedChunk);
    }

    private string CreateProgressBar(double percentage)
    {
        const int barLength = 20;
        var filledLength = (int)(barLength * percentage / 100.0);
        var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
        return $"[{bar}]";
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }

    private async Task RecordFailedStreamingMetric(StreamingContext context, int chunkCount, long totalBytes, string errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var duration = DateTime.UtcNow - context.StartTime;
            var processingRate = totalBytes > 0 && duration.TotalSeconds > 0 ? totalBytes / duration.TotalSeconds : 0;

            var metric = new StreamingMetric
            {
                Duration = duration,
                ChunksProcessed = chunkCount,
                BytesProcessed = totalBytes,
                ErrorCount = context.Errors.Count + 1, // +1 for the current error
                RetryCount = context.RetryCount,
                AverageChunkSize = chunkCount > 0 ? (double)totalBytes / chunkCount : 0,
                ProcessingRate = processingRate,
                Success = false,
                Metadata = new Dictionary<string, object>
                {
                    ["error_message"] = errorMessage,
                    ["errors"] = context.Errors
                }
            };

            await _metricsCollector.RecordStreamingMetricAsync(metric, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record streaming failure metrics");
        }
    }
}

/// <summary>
/// Extension methods for streaming operations
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Convert string enumerable to async enumerable for streaming
    /// </summary>
    public static async IAsyncEnumerable<string> ToAsyncEnumerable(
        this IEnumerable<string> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return item;
            await Task.Yield(); // Allow other tasks to run
        }
    }

    /// <summary>
    /// Simulate streaming by chunking a string
    /// </summary>
    public static async IAsyncEnumerable<string> ToChunkedStream(
        this string content,
        int chunkSize = 100,
        TimeSpan? delay = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content))
            yield break;

        var actualDelay = delay ?? TimeSpan.FromMilliseconds(50);
        
        for (int i = 0; i < content.Length; i += chunkSize)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var chunk = content.Substring(i, Math.Min(chunkSize, content.Length - i));
            yield return chunk;
            
            if (actualDelay > TimeSpan.Zero)
            {
                await Task.Delay(actualDelay, cancellationToken);
            }
        }
    }
}
