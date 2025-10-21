using MCMAA.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MCMAA.AI;

/// <summary>
/// Extension methods for registering AI services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCMAA AI services to the service collection
    /// </summary>
    public static IServiceCollection AddMcmaaAI(this IServiceCollection services)
    {
        services.AddScoped<IAiAssistant, OllamaAiAssistant>();

        return services;
    }
}
