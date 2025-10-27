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

    [Fact]
    public async Task MetricsCollector_CanRecordScanMetric()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var scanMetric = new ScanMetric
        {
            ScanPath = "/test/path",
            Duration = TimeSpan.FromMilliseconds(100),
            FilesScanned = 50,
            DirectoriesScanned = 5,
            ModsFound = 10,
            ConfigFilesFound = 20,
            ResourcePacksFound = 2,
            ErrorCount = 0,
            WarningCount = 1
        };

        // Act
        await metricsCollector.RecordScanMetricAsync(scanMetric);
        var report = await metricsCollector.GetMetricsReportAsync();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalScans);
        Assert.Equal(50, report.TotalFilesScanned);
    }

    [Fact]
    public async Task MetricsCollector_CanRecordAnalysisMetric()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var analysisMetric = new AnalysisMetric
        {
            Model = "phi3:mini",
            TaskType = AnalysisTaskType.Quick,
            Duration = TimeSpan.FromMilliseconds(500),
            TokensUsed = 1000,
            InputTokens = 500,
            OutputTokens = 500,
            FromCache = false,
            StreamingUsed = false,
            Success = true,
            Temperature = 0.7
        };

        // Act
        await metricsCollector.RecordAnalysisMetricAsync(analysisMetric);
        var report = await metricsCollector.GetMetricsReportAsync();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalAnalyses);
        Assert.Equal(1000, report.TotalTokensUsed);
    }

    [Fact]
    public async Task MetricsCollector_CanRecordPreprocessingMetric()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var preprocessingMetric = new PreprocessingMetric
        {
            TaskType = AnalysisTaskType.Full,
            Duration = TimeSpan.FromMilliseconds(200),
            OriginalTokens = 2000,
            OptimizedTokens = 1400,
            CompressionRatio = 0.7,
            HighPrioritySections = 3,
            MediumPrioritySections = 2,
            LowPrioritySections = 1,
            OptimizationSteps = new List<string> { "Prioritized", "Filtered", "Optimized" }
        };

        // Act
        await metricsCollector.RecordPreprocessingMetricAsync(preprocessingMetric);
        var report = await metricsCollector.GetMetricsReportAsync();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalPreprocessingOperations);
        Assert.Equal(0.7, report.AverageCompressionRatio);
    }

    [Fact]
    public async Task MetricsCollector_CanExportMetricsAsJson()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var scanMetric = new ScanMetric
        {
            ScanPath = "/test/path",
            Duration = TimeSpan.FromMilliseconds(100),
            FilesScanned = 50,
            DirectoriesScanned = 5,
            ModsFound = 10,
            ConfigFilesFound = 20,
            ResourcePacksFound = 2,
            ErrorCount = 0,
            WarningCount = 1
        };
        await metricsCollector.RecordScanMetricAsync(scanMetric);

        // Act
        var jsonExport = await metricsCollector.ExportMetricsAsync(MetricsExportFormat.Json);

        // Assert
        Assert.NotNull(jsonExport);
        Assert.NotEmpty(jsonExport);
        Assert.Contains("totalScans", jsonExport);
        Assert.Contains("totalFilesScanned", jsonExport);
    }

    [Fact]
    public async Task MetricsCollector_CanExportMetricsAsCsv()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var scanMetric = new ScanMetric
        {
            ScanPath = "/test/path",
            Duration = TimeSpan.FromMilliseconds(100),
            FilesScanned = 50,
            DirectoriesScanned = 5,
            ModsFound = 10,
            ConfigFilesFound = 20,
            ResourcePacksFound = 2,
            ErrorCount = 0,
            WarningCount = 1
        };
        await metricsCollector.RecordScanMetricAsync(scanMetric);

        // Act
        var csvExport = await metricsCollector.ExportMetricsAsync(MetricsExportFormat.Csv);

        // Assert
        Assert.NotNull(csvExport);
        Assert.NotEmpty(csvExport);
    }

    [Fact]
    public async Task PerformanceTracker_CanTrackOperation()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider();
        var performanceTracker = serviceProvider.GetRequiredService<IPerformanceTracker>();

        // Act
        using (var context = performanceTracker.StartTracking("TestOperation", new Dictionary<string, object> { ["test"] = "value" }))
        {
            await Task.Delay(10);
            context.AddCheckpoint("Checkpoint1");
        }
        var stats = await performanceTracker.GetStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalOperations > 0);
    }
}