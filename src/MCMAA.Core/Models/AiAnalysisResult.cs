namespace MCMAA.Core.Models;

/// <summary>
/// Result of an AI analysis operation
/// </summary>
public class AiAnalysisResult
{
    /// <summary>
    /// Analysis task that was performed
    /// </summary>
    public AnalysisTask Task { get; set; } = new();

    /// <summary>
    /// Model used for analysis
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Generated analysis content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when analysis was performed
    /// </summary>
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the analysis
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Number of tokens used in the request
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Whether the result was retrieved from cache
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Cache key used (if applicable)
    /// </summary>
    public string? CacheKey { get; set; }

    /// <summary>
    /// Temperature used for generation
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Whether streaming was used
    /// </summary>
    public bool StreamingUsed { get; set; }

    /// <summary>
    /// Any errors encountered during analysis
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warnings generated during analysis
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Success status
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Output file path (if saved to file)
    /// </summary>
    public string? OutputFilePath { get; set; }
}
