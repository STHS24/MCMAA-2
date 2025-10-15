namespace MCMAA.Core.Configuration;

/// <summary>
/// Configuration for request timeouts and retry logic
/// </summary>
public class TimeoutConfiguration
{
    /// <summary>
    /// Standard request timeout in seconds
    /// </summary>
    public int RequestStandard { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Large request timeout in seconds
    /// </summary>
    public int RequestLarge { get; set; } = 600; // 10 minutes

    /// <summary>
    /// Complex request timeout in seconds
    /// </summary>
    public int RequestComplex { get; set; } = 900; // 15 minutes

    /// <summary>
    /// Maximum number of retries
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Base delay for exponential backoff in milliseconds
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay for exponential backoff in milliseconds
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;
}
