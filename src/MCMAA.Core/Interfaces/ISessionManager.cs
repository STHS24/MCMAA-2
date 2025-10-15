using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Manages Ollama sessions with connection pooling and health monitoring
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Get or create a session for the specified model
    /// </summary>
    Task<OllamaSession> GetSessionAsync(string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a session back to the pool
    /// </summary>
    Task ReleaseSessionAsync(OllamaSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check health of all active sessions
    /// </summary>
    Task<SessionHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session statistics
    /// </summary>
    Task<SessionStatistics> GetStatisticsAsync();

    /// <summary>
    /// Cleanup expired or unhealthy sessions
    /// </summary>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an Ollama session with connection details
/// </summary>
public class OllamaSession
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime LastUsed { get; set; }
    public bool IsHealthy { get; set; } = true;
    public int RequestCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public HttpClient? HttpClient { get; set; }
}

/// <summary>
/// Health status of session pool
/// </summary>
public class SessionHealthStatus
{
    public int TotalSessions { get; set; }
    public int HealthySessions { get; set; }
    public int UnhealthySessions { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Session pool statistics
/// </summary>
public class SessionStatistics
{
    public int ActiveSessions { get; set; }
    public int TotalSessionsCreated { get; set; }
    public int TotalRequestsProcessed { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public DateTime LastCleanup { get; set; }
    public Dictionary<string, int> SessionsByModel { get; set; } = new();
}
