using MCMAA.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MCMAA.Scanner;

/// <summary>
/// Extension methods for registering scanner services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCMAA scanner services to the service collection
    /// </summary>
    public static IServiceCollection AddMcmaaScanner(this IServiceCollection services)
    {
        // Register as Singleton since ModpackScanner is stateless
        services.AddSingleton<IModpackScanner, ModpackScanner>();

        return services;
    }
}
