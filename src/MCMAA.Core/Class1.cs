using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MCMAA.Core.Configuration;
using MCMAA.Core.Interfaces;
using MCMAA.Core.Services;

namespace MCMAA.Core;

/// <summary>
/// Core configuration and dependency injection setup for MCMAA
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCMAA core services to the service collection
    /// </summary>
    public static IServiceCollection AddMcmaaCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure settings
        services.Configure<AiConfiguration>(configuration.GetSection("AI"));
        services.Configure<CacheConfiguration>(configuration.GetSection("Cache"));
        services.Configure<TimeoutConfiguration>(configuration.GetSection("Timeouts"));

        // Add core services
        services.AddMemoryCache();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<ICacheService, FileCacheService>();
        services.AddSingleton<ISessionManager, OllamaSessionManager>();
        services.AddSingleton<IContentPreprocessor, ContentPreprocessor>();
        services.AddSingleton<IStreamingHandler, StreamingHandler>();

        // Add metrics and performance tracking services
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        services.AddSingleton<IPerformanceTracker, PerformanceTracker>();

        return services;
    }
}
