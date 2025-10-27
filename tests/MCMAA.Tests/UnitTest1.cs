using MCMAA.Core;
using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using MCMAA.Scanner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace MCMAA.Tests;

/// <summary>
/// Tests for core functionality and service integration
/// </summary>
public class CoreFunctionalityTests
{
    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddMcmaaCore(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddMcmaaScanner();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ServiceProvider_CanResolveScanner()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();

        // Act
        var scanner = serviceProvider.GetService<IModpackScanner>();

        // Assert
        Assert.NotNull(scanner);
    }

    [Fact]
    public void ServiceProvider_CanResolveCacheService()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();

        // Act
        var cacheService = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.NotNull(cacheService);
    }

    [Fact]
    public void ServiceProvider_CanResolveMetricsCollector()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();

        // Act
        var metricsCollector = serviceProvider.GetService<IMetricsCollector>();

        // Assert
        Assert.NotNull(metricsCollector);
    }

    [Fact]
    public void ServiceProvider_CanResolvePerformanceTracker()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();

        // Act
        var performanceTracker = serviceProvider.GetService<IPerformanceTracker>();

        // Assert
        Assert.NotNull(performanceTracker);
    }

    [Fact]
    public void AnalysisTaskType_AllValuesAreDefined()
    {
        // Arrange & Act
        var taskTypes = Enum.GetValues(typeof(AnalysisTaskType)).Cast<AnalysisTaskType>().ToList();

        // Assert
        Assert.NotEmpty(taskTypes);
        Assert.Contains(AnalysisTaskType.Full, taskTypes);
        Assert.Contains(AnalysisTaskType.Quick, taskTypes);
        Assert.Contains(AnalysisTaskType.Summary, taskTypes);
        Assert.Contains(AnalysisTaskType.Conflicts, taskTypes);
        Assert.Contains(AnalysisTaskType.Performance, taskTypes);
    }

    [Fact]
    public void TimeoutCategory_AllValuesAreDefined()
    {
        // Arrange & Act
        var categories = Enum.GetValues(typeof(TimeoutCategory)).Cast<TimeoutCategory>().ToList();

        // Assert
        Assert.NotEmpty(categories);
        Assert.Contains(TimeoutCategory.Standard, categories);
        Assert.Contains(TimeoutCategory.Large, categories);
        Assert.Contains(TimeoutCategory.Complex, categories);
    }

    [Fact]
    public void ModpackScanner_CanGetSupportedExtensions()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var scanner = serviceProvider.GetRequiredService<IModpackScanner>();

        // Act
        var extensions = scanner.GetSupportedExtensions();

        // Assert
        Assert.NotEmpty(extensions);
        Assert.Contains(".json", extensions.Keys);
        Assert.Contains(".toml", extensions.Keys);
        Assert.Contains(".yaml", extensions.Keys);
        Assert.Contains(".xml", extensions.Keys);
    }

    [Fact]
    public void ModpackScanner_ValidatesModpackPath()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var scanner = serviceProvider.GetRequiredService<IModpackScanner>();
        var tempDir = Path.Combine(Path.GetTempPath(), "test-modpack");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "mods"));

        try
        {
            // Act
            var isValid = scanner.IsValidModpackPath(tempDir);

            // Assert
            Assert.True(isValid);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ModpackScanner_RejectsInvalidPath()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var scanner = serviceProvider.GetRequiredService<IModpackScanner>();
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());

        // Act
        var isValid = scanner.IsValidModpackPath(invalidPath);

        // Assert
        Assert.False(isValid);
    }
}