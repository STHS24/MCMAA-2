using MCMAA.Core.Configuration;
using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MCMAA.Core.Services;

/// <summary>
/// Manages Ollama sessions with connection pooling and health monitoring
/// </summary>
public class OllamaSessionManager : ISessionManager, IDisposable
{
    private readonly ILogger<OllamaSessionManager> _logger;
    private readonly AiConfiguration _aiConfig;
    private readonly TimeoutConfiguration _timeoutConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    
    private readonly ConcurrentDictionary<string, OllamaSession> _sessions = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _modelSemaphores = new();
    private readonly object _lockObject = new();
    private readonly Timer _healthCheckTimer;
    private readonly Timer _cleanupTimer;
    
    private SessionStatistics _statistics = new();
    private bool _disposed = false;

    public OllamaSessionManager(
        ILogger<OllamaSessionManager> logger,
        IOptions<AiConfiguration> aiConfig,
        IOptions<TimeoutConfiguration> timeoutConfig,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _aiConfig = aiConfig.Value;
        _timeoutConfig = timeoutConfig.Value;
        _httpClientFactory = httpClientFactory;

        // Start health check timer (every 5 minutes)
        _healthCheckTimer = new Timer(async _ => await CheckHealthAsync(), null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        // Start cleanup timer (every 10 minutes)
        _cleanupTimer = new Timer(async _ => await CleanupAsync(), null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        _logger.LogDebug("Session manager initialized");
    }

    public async Task<OllamaSession> GetSessionAsync(string model, CancellationToken cancellationToken = default)
    {
        // Get or create semaphore for this model
        var semaphore = _modelSemaphores.GetOrAdd(model, _ => new SemaphoreSlim(3, 3)); // Max 3 concurrent sessions per model
        
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Try to find an existing healthy session for this model
            var existingSession = _sessions.Values
                .Where(s => s.Model == model && s.IsHealthy)
                .OrderBy(s => s.LastUsed)
                .FirstOrDefault();

            if (existingSession != null)
            {
                existingSession.LastUsed = DateTime.UtcNow;
                _logger.LogDebug("Reusing existing session {SessionId} for model {Model}", 
                    existingSession.Id, model);
                return existingSession;
            }

            // Create new session
            var session = await CreateSessionAsync(model, cancellationToken);
            _sessions.TryAdd(session.Id, session);

            lock (_lockObject)
            {
                _statistics.TotalSessionsCreated++;
                _statistics.ActiveSessions = _sessions.Count;
                if (!_statistics.SessionsByModel.ContainsKey(model))
                    _statistics.SessionsByModel[model] = 0;
                _statistics.SessionsByModel[model]++;
            }

            _logger.LogDebug("Created new session {SessionId} for model {Model}", session.Id, model);
            return session;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task ReleaseSessionAsync(OllamaSession session, CancellationToken cancellationToken = default)
    {
        if (session == null) return Task.CompletedTask;

        session.LastUsed = DateTime.UtcNow;

        lock (_lockObject)
        {
            _statistics.TotalRequestsProcessed++;
        }

        _logger.LogDebug("Released session {SessionId} for model {Model}", session.Id, session.Model);
        return Task.CompletedTask;
    }

    public async Task<SessionHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var healthStatus = new SessionHealthStatus
        {
            LastHealthCheck = DateTime.UtcNow,
            TotalSessions = _sessions.Count
        };

        var healthyCount = 0;
        var unhealthyCount = 0;
        var issues = new List<string>();

        var healthCheckTasks = _sessions.Values.Select(async session =>
        {
            try
            {
                var isHealthy = await CheckSessionHealthAsync(session, cancellationToken);
                session.IsHealthy = isHealthy;

                if (isHealthy)
                {
                    Interlocked.Increment(ref healthyCount);
                }
                else
                {
                    Interlocked.Increment(ref unhealthyCount);
                    lock (issues)
                    {
                        issues.Add($"Session {session.Id} for model {session.Model} is unhealthy");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for session {SessionId}", session.Id);
                session.IsHealthy = false;
                Interlocked.Increment(ref unhealthyCount);
                lock (issues)
                {
                    issues.Add($"Session {session.Id} health check failed: {ex.Message}");
                }
            }
        });

        await Task.WhenAll(healthCheckTasks);

        healthStatus.HealthySessions = healthyCount;
        healthStatus.UnhealthySessions = unhealthyCount;
        healthStatus.Issues = issues;

        _logger.LogDebug("Health check completed: {Healthy}/{Total} sessions healthy",
            healthStatus.HealthySessions, healthStatus.TotalSessions);

        return healthStatus;
    }

    public Task<SessionStatistics> GetStatisticsAsync()
    {
        lock (_lockObject)
        {
            var stats = new SessionStatistics
            {
                ActiveSessions = _sessions.Count,
                TotalSessionsCreated = _statistics.TotalSessionsCreated,
                TotalRequestsProcessed = _statistics.TotalRequestsProcessed,
                LastCleanup = _statistics.LastCleanup,
                SessionsByModel = new Dictionary<string, int>(_statistics.SessionsByModel)
            };

            if (!_sessions.IsEmpty)
            {
                var totalDuration = _sessions.Values.Sum(s => s.TotalDuration.TotalMilliseconds);
                stats.AverageSessionDuration = TimeSpan.FromMilliseconds(totalDuration / _sessions.Count);
            }

            return Task.FromResult(stats);
        }
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Remove sessions unused for 30 minutes
        var sessionsToRemove = new List<string>();

        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;
            if (session.LastUsed < cutoffTime || !session.IsHealthy)
            {
                sessionsToRemove.Add(kvp.Key);
            }
        }

        foreach (var sessionId in sessionsToRemove)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                try
                {
                    session.HttpClient?.Dispose();
                    _logger.LogDebug("Cleaned up session {SessionId} for model {Model}", 
                        sessionId, session.Model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing session {SessionId}", sessionId);
                }
            }
        }

        lock (_lockObject)
        {
            _statistics.ActiveSessions = _sessions.Count;
            _statistics.LastCleanup = DateTime.UtcNow;
        }

        _logger.LogDebug("Cleanup completed: removed {Count} sessions", sessionsToRemove.Count);
    }

    private async Task<OllamaSession> CreateSessionAsync(string model, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(_aiConfig.OllamaBaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(_timeoutConfig.RequestStandard);

        var session = new OllamaSession
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Model = model,
            BaseUrl = _aiConfig.OllamaBaseUrl,
            Created = DateTime.UtcNow,
            LastUsed = DateTime.UtcNow,
            IsHealthy = true,
            HttpClient = httpClient
        };

        // Verify the session can connect to Ollama
        var isHealthy = await CheckSessionHealthAsync(session, cancellationToken);
        session.IsHealthy = isHealthy;

        if (!isHealthy)
        {
            httpClient.Dispose();
            throw new InvalidOperationException($"Failed to create healthy session for model {model}");
        }

        return session;
    }

    private async Task<bool> CheckSessionHealthAsync(OllamaSession session, CancellationToken cancellationToken)
    {
        try
        {
            if (session.HttpClient == null) return false;

            var response = await session.HttpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health check failed for session {SessionId}", session.Id);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _healthCheckTimer?.Dispose();
        _cleanupTimer?.Dispose();

        foreach (var session in _sessions.Values)
        {
            try
            {
                session.HttpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing session {SessionId}", session.Id);
            }
        }

        foreach (var semaphore in _modelSemaphores.Values)
        {
            semaphore.Dispose();
        }

        _sessions.Clear();
        _modelSemaphores.Clear();
        _disposed = true;

        _logger.LogDebug("Session manager disposed");
    }
}
