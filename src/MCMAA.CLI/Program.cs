using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MCMAA.Core;
using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using MCMAA.Scanner;
using MCMAA.AI;

namespace MCMAA.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMcmaaCore(configuration);
        services.AddMcmaaScanner();
        services.AddMcmaaAI();
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Create root command
        var rootCommand = CreateRootCommand(serviceProvider);

        // Execute command
        return await rootCommand.InvokeAsync(args);
    }

    private static RootCommand CreateRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("🎮 Minecraft Modpack Config AI Assistant (MCMAA)")
        {
            Description = "AI-powered assistant for analyzing and optimizing Minecraft modpack configurations"
        };

        // Path argument
        var pathArgument = new Argument<string>(
            name: "path",
            description: "Path to the modpack directory to analyze");

        // Options
        var taskOption = new Option<string>(
            name: "--task",
            description: "Analysis task to perform",
            getDefaultValue: () => "full")
        {
            AllowMultipleArgumentsPerToken = false
        };
        taskOption.AddAlias("-t");
        taskOption.FromAmong("full", "quick", "summary", "conflicts", "performance");

        var modelOption = new Option<string?>(
            name: "--model",
            description: "Override the AI model to use")
        {
            AllowMultipleArgumentsPerToken = false
        };
        modelOption.AddAlias("-m");

        var noCacheOption = new Option<bool>(
            name: "--no-cache",
            description: "Skip cache and force fresh analysis",
            getDefaultValue: () => false);

        var clearCacheOption = new Option<bool>(
            name: "--clear-cache",
            description: "Clear cache before analysis",
            getDefaultValue: () => false);

        var noStreamingOption = new Option<bool>(
            name: "--no-streaming",
            description: "Disable streaming output",
            getDefaultValue: () => false);

        var statsOption = new Option<bool>(
            name: "--stats",
            description: "Show performance statistics",
            getDefaultValue: () => false);

        var metricsOption = new Option<bool>(
            name: "--metrics",
            description: "Show detailed metrics report",
            getDefaultValue: () => false);

        var exportMetricsOption = new Option<string?>(
            name: "--export-metrics",
            description: "Export metrics to file (json, csv, xml)")
        {
            AllowMultipleArgumentsPerToken = false
        };

        var outputOption = new Option<string?>(
            name: "--output",
            description: "Output file path for the analysis result")
        {
            AllowMultipleArgumentsPerToken = false
        };
        outputOption.AddAlias("-o");

        // Add arguments and options to root command
        rootCommand.AddArgument(pathArgument);
        rootCommand.AddOption(taskOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(noCacheOption);
        rootCommand.AddOption(clearCacheOption);
        rootCommand.AddOption(noStreamingOption);
        rootCommand.AddOption(statsOption);
        rootCommand.AddOption(metricsOption);
        rootCommand.AddOption(exportMetricsOption);
        rootCommand.AddOption(outputOption);

        // Set handler
        rootCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var task = context.ParseResult.GetValueForOption(taskOption);
            var model = context.ParseResult.GetValueForOption(modelOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var clearCache = context.ParseResult.GetValueForOption(clearCacheOption);
            var noStreaming = context.ParseResult.GetValueForOption(noStreamingOption);
            var stats = context.ParseResult.GetValueForOption(statsOption);
            var metrics = context.ParseResult.GetValueForOption(metricsOption);
            var exportMetrics = context.ParseResult.GetValueForOption(exportMetricsOption);
            var output = context.ParseResult.GetValueForOption(outputOption);

            await HandleAnalysisCommand(serviceProvider, path!, task!, model, noCache, clearCache, noStreaming, stats, metrics, exportMetrics, output);
        });

        return rootCommand;
    }

    private static async Task HandleAnalysisCommand(
        IServiceProvider serviceProvider,
        string path,
        string task,
        string? model,
        bool noCache,
        bool clearCache,
        bool noStreaming,
        bool stats,
        bool metrics,
        string? exportMetrics,
        string? output)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var scanner = serviceProvider.GetRequiredService<IModpackScanner>();
        var aiAssistant = serviceProvider.GetRequiredService<IAiAssistant>();
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var metricsCollector = serviceProvider.GetRequiredService<IMetricsCollector>();
        var performanceTracker = serviceProvider.GetRequiredService<IPerformanceTracker>();

        try
        {
            // Validate path
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"❌ Error: Directory does not exist: {path}");
                return;
            }

            if (!scanner.IsValidModpackPath(path))
            {
                Console.WriteLine($"⚠️  Warning: Path does not appear to be a valid modpack directory: {path}");
                Console.WriteLine("Continuing with analysis...");
            }

            // Clear cache if requested
            if (clearCache)
            {
                Console.WriteLine("🧹 Clearing cache...");
                await cacheService.ClearAsync();
            }

            // Check AI service availability
            Console.WriteLine("🔍 Checking AI service availability...");
            if (!await aiAssistant.IsAvailableAsync())
            {
                Console.WriteLine("❌ Error: AI service (Ollama) is not available. Please ensure Ollama is running.");
                return;
            }

            // Parse task type
            if (!Enum.TryParse<AnalysisTaskType>(task, true, out var taskType))
            {
                Console.WriteLine($"❌ Error: Invalid task type: {task}");
                return;
            }

            // Create analysis task
            var analysisTask = CreateAnalysisTask(taskType);

            Console.WriteLine($"🚀 Starting modpack analysis...");
            Console.WriteLine($"   Path: {path}");
            Console.WriteLine($"   Task: {task}");
            Console.WriteLine($"   Model: {model ?? aiAssistant.GetRecommendedModel(taskType)}");
            Console.WriteLine();

            // Perform scan
            Console.WriteLine("📁 Scanning modpack directory...");
            var scanResult = await scanner.ScanAsync(path);

            if (scanResult.Errors.Any())
            {
                Console.WriteLine("❌ Scan completed with errors:");
                foreach (var error in scanResult.Errors)
                {
                    Console.WriteLine($"   • {error}");
                }
                return;
            }

            Console.WriteLine($"✅ Scan completed successfully!");
            Console.WriteLine($"   Files: {scanResult.TotalFiles}");
            Console.WriteLine($"   Mods: {scanResult.Mods.Count}");
            Console.WriteLine($"   Configs: {scanResult.ConfigFiles.Count}");
            Console.WriteLine($"   Resource Packs: {scanResult.ResourcePacks.Count}");
            Console.WriteLine();

            // Perform AI analysis
            Console.WriteLine("🤖 Starting AI analysis...");

            AiAnalysisResult analysisResult;

            if (noStreaming)
            {
                analysisResult = await aiAssistant.AnalyzeAsync(scanResult, analysisTask, model);
            }
            else
            {
                analysisResult = await aiAssistant.AnalyzeStreamingAsync(
                    scanResult,
                    analysisTask,
                    chunk => Console.Write(chunk),
                    model);
                Console.WriteLine(); // New line after streaming
            }

            if (!analysisResult.Success)
            {
                Console.WriteLine("❌ AI analysis failed:");
                foreach (var error in analysisResult.Errors)
                {
                    Console.WriteLine($"   • {error}");
                }
                return;
            }

            Console.WriteLine();
            Console.WriteLine("✅ AI analysis completed successfully!");

            // Save output if requested
            if (!string.IsNullOrEmpty(output))
            {
                await SaveAnalysisResult(analysisResult, output);
                Console.WriteLine($"💾 Analysis saved to: {output}");
            }

            // Show statistics if requested
            if (stats)
            {
                await ShowStatistics(analysisResult, cacheService);
            }

            // Show metrics if requested
            if (metrics)
            {
                await ShowMetrics(metricsCollector, performanceTracker);
            }

            // Export metrics if requested
            if (!string.IsNullOrEmpty(exportMetrics))
            {
                await ExportMetrics(metricsCollector, exportMetrics);
            }

            // Show warnings if any
            if (scanResult.Warnings.Any() || analysisResult.Warnings.Any())
            {
                Console.WriteLine();
                Console.WriteLine("⚠️  Warnings:");
                foreach (var warning in scanResult.Warnings.Concat(analysisResult.Warnings))
                {
                    Console.WriteLine($"   • {warning}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error during analysis");
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
        }
    }

    private static AnalysisTask CreateAnalysisTask(AnalysisTaskType taskType)
    {
        return taskType switch
        {
            AnalysisTaskType.Full => new AnalysisTask
            {
                Type = taskType,
                Name = "Full Analysis",
                Description = "Comprehensive modpack analysis with detailed insights",
                TimeoutCategory = TimeoutCategory.Complex,
                Priority = 10
            },
            AnalysisTaskType.Quick => new AnalysisTask
            {
                Type = taskType,
                Name = "Quick Analysis",
                Description = "Fast overview of modpack structure and key components",
                TimeoutCategory = TimeoutCategory.Standard,
                Priority = 5
            },
            AnalysisTaskType.Summary => new AnalysisTask
            {
                Type = taskType,
                Name = "Summary",
                Description = "Brief summary of modpack contents",
                TimeoutCategory = TimeoutCategory.Standard,
                Priority = 3
            },
            AnalysisTaskType.Conflicts => new AnalysisTask
            {
                Type = taskType,
                Name = "Conflict Detection",
                Description = "Identify potential mod conflicts and compatibility issues",
                TimeoutCategory = TimeoutCategory.Large,
                Priority = 8
            },
            AnalysisTaskType.Performance => new AnalysisTask
            {
                Type = taskType,
                Name = "Performance Analysis",
                Description = "Analyze performance implications and optimization opportunities",
                TimeoutCategory = TimeoutCategory.Large,
                Priority = 7
            },
            _ => throw new ArgumentException($"Unknown task type: {taskType}")
        };
    }

    private static async Task SaveAnalysisResult(AiAnalysisResult result, string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = $"# {result.Task.Name}\n\n";
        content += $"**Generated:** {result.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss} UTC\n";
        content += $"**Model:** {result.ModelUsed}\n";
        content += $"**Duration:** {result.Duration.TotalSeconds:F2} seconds\n";
        content += $"**From Cache:** {(result.FromCache ? "Yes" : "No")}\n\n";
        content += "---\n\n";
        content += result.Content;

        await File.WriteAllTextAsync(outputPath, content);
    }

    private static async Task ShowStatistics(AiAnalysisResult analysisResult, ICacheService cacheService)
    {
        Console.WriteLine();
        Console.WriteLine("📊 Performance Statistics:");
        Console.WriteLine($"   Duration: {analysisResult.Duration.TotalSeconds:F2} seconds");
        Console.WriteLine($"   Model: {analysisResult.ModelUsed}");
        Console.WriteLine($"   Temperature: {analysisResult.Temperature}");
        Console.WriteLine($"   Tokens Used: {analysisResult.TokensUsed}");
        Console.WriteLine($"   From Cache: {(analysisResult.FromCache ? "Yes" : "No")}");
        Console.WriteLine($"   Streaming: {(analysisResult.StreamingUsed ? "Yes" : "No")}");

        var cacheStats = await cacheService.GetStatisticsAsync();
        Console.WriteLine();
        Console.WriteLine("💾 Cache Statistics:");
        Console.WriteLine($"   Total Entries: {cacheStats.TotalEntries}");
        Console.WriteLine($"   Total Size: {FormatBytes(cacheStats.TotalSizeBytes)}");
        Console.WriteLine($"   Hit Ratio: {cacheStats.HitRatio:P1}");
        Console.WriteLine($"   Total Requests: {cacheStats.TotalRequests}");
        Console.WriteLine($"   Last Cleanup: {cacheStats.LastCleanup:yyyy-MM-dd HH:mm:ss}");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static async Task ShowMetrics(IMetricsCollector metricsCollector, IPerformanceTracker performanceTracker)
    {
        Console.WriteLine();
        Console.WriteLine("📊 Detailed Metrics Report:");
        Console.WriteLine("═══════════════════════════");

        try
        {
            // Get metrics report
            var metricsReport = await metricsCollector.GetMetricsReportAsync();

            // Scan metrics
            if (metricsReport.TotalScans > 0)
            {
                Console.WriteLine();
                Console.WriteLine("🔍 Scan Metrics:");
                Console.WriteLine($"   Total Scans: {metricsReport.TotalScans}");
                Console.WriteLine($"   Average Duration: {metricsReport.AverageScanDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Files Scanned: {metricsReport.TotalFilesScanned:N0}");
                Console.WriteLine($"   Data Processed: {FormatBytes(metricsReport.TotalBytesScanned)}");
                Console.WriteLine($"   Errors: {metricsReport.TotalErrors}");
                Console.WriteLine($"   Warnings: {metricsReport.TotalWarnings}");
            }

            // Analysis metrics
            if (metricsReport.TotalAnalyses > 0)
            {
                Console.WriteLine();
                Console.WriteLine("🤖 AI Analysis Metrics:");
                Console.WriteLine($"   Total Analyses: {metricsReport.TotalAnalyses}");
                Console.WriteLine($"   Average Duration: {metricsReport.AverageAnalysisDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Tokens Used: {metricsReport.TotalTokensUsed:N0}");
                Console.WriteLine($"   Cache Hit Rate: {metricsReport.CacheHitRate:P1}");
                Console.WriteLine($"   Streaming Usage: {metricsReport.StreamingUsageRate:P1}");

                if (metricsReport.ModelUsage.Any())
                {
                    Console.WriteLine("   Model Usage:");
                    foreach (var model in metricsReport.ModelUsage.OrderByDescending(m => m.Value))
                    {
                        Console.WriteLine($"     • {model.Key}: {model.Value} times");
                    }
                }
            }

            // Preprocessing metrics
            if (metricsReport.TotalPreprocessingOperations > 0)
            {
                Console.WriteLine();
                Console.WriteLine("⚙️ Preprocessing Metrics:");
                Console.WriteLine($"   Total Operations: {metricsReport.TotalPreprocessingOperations}");
                Console.WriteLine($"   Average Duration: {metricsReport.AveragePreprocessingDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Average Compression: {metricsReport.AverageCompressionRatio:P1}");
                Console.WriteLine($"   Tokens Optimized: {metricsReport.TotalTokensOptimized:N0}");
            }

            // Streaming metrics
            if (metricsReport.TotalStreamingOperations > 0)
            {
                Console.WriteLine();
                Console.WriteLine("📡 Streaming Metrics:");
                Console.WriteLine($"   Total Operations: {metricsReport.TotalStreamingOperations}");
                Console.WriteLine($"   Average Duration: {metricsReport.AverageStreamingDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Data Streamed: {FormatBytes(metricsReport.TotalBytesStreamed)}");
                Console.WriteLine($"   Average Rate: {FormatBytes((long)metricsReport.AverageStreamingRate)}/s");
                Console.WriteLine($"   Errors: {metricsReport.TotalStreamingErrors}");
            }

            // System metrics
            if (metricsReport.SystemMetrics.Any())
            {
                Console.WriteLine();
                Console.WriteLine("💻 System Metrics:");
                foreach (var metric in metricsReport.SystemMetrics)
                {
                    var value = metric.Value switch
                    {
                        double d => $"{d:F1}",
                        int i => $"{i:N0}",
                        _ => metric.Value.ToString()
                    };
                    Console.WriteLine($"   {metric.Key.Replace("_", " ").ToTitleCase()}: {value}");
                }
            }

            // Top errors
            if (metricsReport.TopErrors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("❌ Most Frequent Errors:");
                foreach (var error in metricsReport.TopErrors.Take(3))
                {
                    Console.WriteLine($"   • {error}");
                }
            }

            // Performance statistics
            var perfStats = await performanceTracker.GetStatisticsAsync();
            if (perfStats.TotalOperations > 0)
            {
                Console.WriteLine();
                Console.WriteLine("⚡ Performance Statistics:");
                Console.WriteLine($"   Total Operations: {perfStats.TotalOperations}");
                Console.WriteLine($"   Success Rate: {perfStats.SuccessRate:P1}");
                Console.WriteLine($"   Average Duration: {perfStats.AverageDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   Min/Max Duration: {perfStats.MinDuration.TotalMilliseconds:F0}ms / {perfStats.MaxDuration.TotalMilliseconds:F0}ms");

                if (perfStats.SlowestOperations.Any())
                {
                    Console.WriteLine("   Slowest Operations:");
                    foreach (var op in perfStats.SlowestOperations.Take(3))
                    {
                        Console.WriteLine($"     • {op}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error displaying metrics: {ex.Message}");
        }
    }

    private static async Task ExportMetrics(IMetricsCollector metricsCollector, string exportPath)
    {
        try
        {
            var extension = Path.GetExtension(exportPath).ToLowerInvariant();
            var format = extension switch
            {
                ".json" => MetricsExportFormat.Json,
                ".csv" => MetricsExportFormat.Csv,
                ".xml" => MetricsExportFormat.Xml,
                _ => MetricsExportFormat.Json
            };

            var exportData = await metricsCollector.ExportMetricsAsync(format);
            await File.WriteAllTextAsync(exportPath, exportData);

            Console.WriteLine($"📊 Metrics exported to: {exportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error exporting metrics: {ex.Message}");
        }
    }
}

// Extension method for string formatting
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }
}
