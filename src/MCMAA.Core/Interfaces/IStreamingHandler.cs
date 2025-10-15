using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for enhanced streaming output handling
/// </summary>
public interface IStreamingHandler
{
    /// <summary>
    /// Process streaming chunks with progress tracking
    /// </summary>
    Task ProcessStreamAsync(
        IAsyncEnumerable<string> chunks,
        StreamingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create progress callback for streaming operations
    /// </summary>
    Action<StreamingProgress> CreateProgressCallback(StreamingOptions options);

    /// <summary>
    /// Handle streaming errors gracefully
    /// </summary>
    Task<bool> HandleStreamingErrorAsync(Exception error, StreamingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimize chunk processing for memory efficiency
    /// </summary>
    Task<string> ProcessChunkAsync(string chunk, StreamingContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for streaming operations
/// </summary>
public class StreamingOptions
{
    public bool ShowProgress { get; set; } = true;
    public bool WriteToFile { get; set; } = false;
    public string? OutputPath { get; set; }
    public int ChunkSize { get; set; } = 1024;
    public bool EnableErrorRecovery { get; set; } = true;
    public TimeSpan ProgressUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public Action<string>? OnChunk { get; set; }
    public Action<StreamingProgress>? OnProgress { get; set; }
    public Action<Exception>? OnError { get; set; }
}

/// <summary>
/// Progress information for streaming operations
/// </summary>
public class StreamingProgress
{
    public int ChunksProcessed { get; set; }
    public int TotalChunks { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public TimeSpan Elapsed { get; set; }
    public double PercentComplete { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Context for streaming operations
/// </summary>
public class StreamingContext
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public AnalysisTask? Task { get; set; }
    public int RetryCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
