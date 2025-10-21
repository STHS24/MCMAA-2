using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using MCMAA.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace MCMAA.Core.Services;

/// <summary>
/// Content preprocessor for optimizing AI input
/// </summary>
public class ContentPreprocessor : IContentPreprocessor
{
    private readonly ILogger<ContentPreprocessor> _logger;
    private readonly AiConfiguration _aiConfig;
    private readonly IMetricsCollector _metricsCollector;
    private readonly IPerformanceTracker _performanceTracker;

    // Approximate tokens per character ratio (varies by model)
    private const double TokensPerCharacter = 0.25;

    // Content compression targets
    private const double TargetCompressionRatio = 0.7; // 30% reduction
    private const int MaxSummaryLength = 500;

    public ContentPreprocessor(
        ILogger<ContentPreprocessor> logger,
        IOptions<AiConfiguration> aiConfig,
        IMetricsCollector metricsCollector,
        IPerformanceTracker performanceTracker)
    {
        _logger = logger;
        _aiConfig = aiConfig.Value;
        _metricsCollector = metricsCollector;
        _performanceTracker = performanceTracker;
    }

    public async Task<PreprocessedContent> PreprocessAsync(
        ScanResult scanResult,
        AnalysisTask task,
        CancellationToken cancellationToken = default)
    {
        using var performanceContext = _performanceTracker.StartTracking("ContentPreprocessing", new Dictionary<string, object>
        {
            ["task_type"] = task.Type.ToString(),
            ["scan_path"] = scanResult.ScanPath
        });

        _logger.LogDebug("Starting content preprocessing for task {TaskType}", task.Type);

        var startTime = DateTime.UtcNow;
        var originalContent = GenerateFullContent(scanResult);
        var originalTokens = EstimateTokenCount(originalContent);

        performanceContext.AddMetadata("original_tokens", originalTokens);
        performanceContext.AddCheckpoint("ContentGenerated");
        
        var result = new PreprocessedContent
        {
            OriginalTokens = originalTokens,
            OptimizationSteps = new List<string>()
        };

        try
        {
            // Step 1: Prioritize sections based on task
            var prioritizedSections = await PrioritizeSectionsAsync(scanResult, task, cancellationToken);
            result.Sections = prioritizedSections;
            result.OptimizationSteps.Add("Prioritized content sections");

            // Step 2: Generate optimized content
            var optimizedContent = await GenerateOptimizedContentAsync(prioritizedSections, task, cancellationToken);
            result.OptimizationSteps.Add("Generated optimized content structure");

            // Step 3: Apply content filtering
            var filteredContent = await FilterContentAsync(optimizedContent, task, cancellationToken);
            result.OptimizationSteps.Add("Applied content filtering");

            // Step 4: Ensure token limits
            var maxTokens = GetMaxTokensForTask(task);
            var finalContent = await OptimizeContentAsync(filteredContent, maxTokens, cancellationToken);
            result.OptimizationSteps.Add($"Optimized to {maxTokens} token limit");

            result.Content = finalContent;
            result.EstimatedTokens = EstimateTokenCount(finalContent);
            result.CompressionRatio = (double)result.EstimatedTokens / originalTokens;
            
            var duration = DateTime.UtcNow - startTime;
            result.Metadata["processing_time_ms"] = duration.TotalMilliseconds;
            result.Metadata["original_length"] = originalContent.Length;
            result.Metadata["final_length"] = finalContent.Length;

            // Record metrics
            var metric = new PreprocessingMetric
            {
                TaskType = task.Type,
                Duration = duration,
                OriginalTokens = originalTokens,
                OptimizedTokens = result.EstimatedTokens,
                CompressionRatio = result.CompressionRatio,
                HighPrioritySections = result.Sections.HighPriority.Count,
                MediumPrioritySections = result.Sections.MediumPriority.Count,
                LowPrioritySections = result.Sections.LowPriority.Count,
                OptimizationSteps = new List<string>(result.OptimizationSteps)
            };

            await _metricsCollector.RecordPreprocessingMetricAsync(metric, cancellationToken);

            performanceContext.AddMetadata("optimized_tokens", result.EstimatedTokens);
            performanceContext.AddMetadata("compression_ratio", result.CompressionRatio);
            performanceContext.AddCheckpoint("MetricsRecorded");

            _logger.LogPreprocessingOperation(
                task.Type.ToString(),
                duration,
                originalTokens,
                result.EstimatedTokens,
                result.CompressionRatio);

            return result;
        }
        catch (Exception ex)
        {
            performanceContext.MarkFailed(ex);
            _logger.LogError(ex, "Error during content preprocessing");

            // Record failed metrics
            var duration = DateTime.UtcNow - startTime;
            var failedMetric = new PreprocessingMetric
            {
                TaskType = task.Type,
                Duration = duration,
                OriginalTokens = originalTokens,
                OptimizedTokens = originalTokens, // No optimization occurred
                CompressionRatio = 1.0,
                OptimizationSteps = new List<string> { $"Failed: {ex.Message}" }
            };

            try
            {
                await _metricsCollector.RecordPreprocessingMetricAsync(failedMetric, cancellationToken);
            }
            catch (Exception metricsEx)
            {
                _logger.LogWarning(metricsEx, "Failed to record preprocessing failure metrics");
            }

            // Fallback to basic content
            result.Content = originalContent;
            result.EstimatedTokens = originalTokens;
            result.CompressionRatio = 1.0;
            result.OptimizationSteps.Add($"Preprocessing failed: {ex.Message}");

            return result;
        }
    }

    public int EstimateTokenCount(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        // Simple token estimation based on character count and word boundaries
        var wordCount = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var charBasedEstimate = (int)(content.Length * TokensPerCharacter);
        
        // Use the higher estimate to be conservative
        return Math.Max(wordCount, charBasedEstimate);
    }

    public Task<string> OptimizeContentAsync(string content, int maxTokens, CancellationToken cancellationToken = default)
    {
        var currentTokens = EstimateTokenCount(content);
        
        if (currentTokens <= maxTokens)
        {
            _logger.LogDebug("Content already within token limit: {CurrentTokens}/{MaxTokens}", currentTokens, maxTokens);
            return Task.FromResult(content);
        }

        _logger.LogDebug("Optimizing content: {CurrentTokens} -> {MaxTokens} tokens", currentTokens, maxTokens);

        var lines = content.Split('\n');
        var result = new StringBuilder();
        var currentTokenCount = 0;
        var targetRatio = (double)maxTokens / currentTokens;

        foreach (var line in lines)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var lineTokens = EstimateTokenCount(line);
            
            if (currentTokenCount + lineTokens > maxTokens)
            {
                // Try to truncate the line if it's very long
                if (lineTokens > maxTokens * 0.1) // If line is more than 10% of limit
                {
                    var truncatedLine = TruncateLine(line, (int)(lineTokens * targetRatio));
                    var truncatedTokens = EstimateTokenCount(truncatedLine);
                    
                    if (currentTokenCount + truncatedTokens <= maxTokens)
                    {
                        result.AppendLine(truncatedLine);
                        currentTokenCount += truncatedTokens;
                    }
                }
                break;
            }

            result.AppendLine(line);
            currentTokenCount += lineTokens;
        }

        if (currentTokenCount < maxTokens * 0.9) // If we have room, add a truncation notice
        {
            result.AppendLine();
            result.AppendLine("[Content truncated to fit token limits]");
        }

        return Task.FromResult(result.ToString());
    }

    public Task<PrioritizedSections> PrioritizeSectionsAsync(
        ScanResult scanResult,
        AnalysisTask task,
        CancellationToken cancellationToken = default)
    {
        var sections = new PrioritizedSections();
        
        // Priority rules based on task type
        var modPriority = GetPriorityForContent(ContentSectionType.ModList, task.Type);
        var configPriority = GetPriorityForContent(ContentSectionType.ConfigFile, task.Type);
        var resourcePriority = GetPriorityForContent(ContentSectionType.ResourcePack, task.Type);

        // Create mod sections
        if (scanResult.Mods.Any())
        {
            var modSection = new ContentSection
            {
                Name = "Mods",
                Type = ContentSectionType.ModList,
                Content = GenerateModListContent(scanResult.Mods),
                Priority = modPriority
            };
            modSection.EstimatedTokens = EstimateTokenCount(modSection.Content);
            
            AddSectionToPriority(sections, modSection);
        }

        // Create config sections
        foreach (var configGroup in scanResult.ConfigFiles.GroupBy(c => Path.GetDirectoryName(c.FilePath) ?? "root"))
        {
            var configSection = new ContentSection
            {
                Name = $"Config: {configGroup.Key}",
                Type = ContentSectionType.ConfigFile,
                Content = GenerateConfigContent(configGroup),
                Priority = configPriority
            };
            configSection.EstimatedTokens = EstimateTokenCount(configSection.Content);
            
            AddSectionToPriority(sections, configSection);
        }

        // Create resource pack sections
        if (scanResult.ResourcePacks.Any())
        {
            var resourceSection = new ContentSection
            {
                Name = "Resource Packs",
                Type = ContentSectionType.ResourcePack,
                Content = GenerateResourcePackContent(scanResult.ResourcePacks),
                Priority = resourcePriority
            };
            resourceSection.EstimatedTokens = EstimateTokenCount(resourceSection.Content);
            
            AddSectionToPriority(sections, resourceSection);
        }

        // Add summary section
        var summarySection = new ContentSection
        {
            Name = "Summary",
            Type = ContentSectionType.Summary,
            Content = GenerateSummaryContent(scanResult),
            Priority = 10 // Always high priority
        };
        summarySection.EstimatedTokens = EstimateTokenCount(summarySection.Content);
        AddSectionToPriority(sections, summarySection);

        sections.TotalSections = sections.HighPriority.Count + sections.MediumPriority.Count + sections.LowPriority.Count;
        sections.SectionCounts = new Dictionary<string, int>
        {
            ["high"] = sections.HighPriority.Count,
            ["medium"] = sections.MediumPriority.Count,
            ["low"] = sections.LowPriority.Count
        };

        _logger.LogDebug("Prioritized {TotalSections} sections: {High} high, {Medium} medium, {Low} low priority",
            sections.TotalSections, sections.HighPriority.Count, sections.MediumPriority.Count, sections.LowPriority.Count);

        return Task.FromResult(sections);
    }

    public Task<string> FilterContentAsync(string content, AnalysisTask task, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content))
            return Task.FromResult(content);

        var lines = content.Split('\n');
        var filteredLines = new List<string>();

        foreach (var line in lines)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Skip empty lines and excessive whitespace
            if (string.IsNullOrWhiteSpace(line))
            {
                if (filteredLines.Count > 0 && !string.IsNullOrWhiteSpace(filteredLines.Last()))
                {
                    filteredLines.Add(string.Empty); // Keep single empty line for formatting
                }
                continue;
            }

            // Filter based on task type
            if (ShouldIncludeLine(line, task.Type))
            {
                filteredLines.Add(line.Trim());
            }
        }

        return Task.FromResult(string.Join('\n', filteredLines));
    }

    private string GenerateFullContent(ScanResult scanResult)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Modpack Analysis");
        sb.AppendLine($"**Path:** {scanResult.ScanPath}");
        sb.AppendLine($"**Scan Date:** {scanResult.ScanTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Files:** {scanResult.TotalFiles}");
        sb.AppendLine();
        
        if (scanResult.Mods.Any())
        {
            sb.AppendLine("## Mods");
            foreach (var mod in scanResult.Mods)
            {
                sb.AppendLine($"- **{mod.Name}** ({mod.Version}) - {mod.FilePath}");
            }
            sb.AppendLine();
        }

        if (scanResult.ConfigFiles.Any())
        {
            sb.AppendLine("## Configuration Files");
            foreach (var config in scanResult.ConfigFiles.Take(20)) // Limit for preprocessing
            {
                sb.AppendLine($"- **{config.Name}** ({config.FileType}) - {config.FilePath}");
                if (!string.IsNullOrEmpty(config.Preview))
                {
                    sb.AppendLine($"  Preview: {config.Preview.Substring(0, Math.Min(100, config.Preview.Length))}...");
                }
            }
            sb.AppendLine();
        }

        if (scanResult.ResourcePacks.Any())
        {
            sb.AppendLine("## Resource Packs");
            foreach (var pack in scanResult.ResourcePacks)
            {
                sb.AppendLine($"- **{pack.Name}** - {pack.FilePath}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private Task<string> GenerateOptimizedContentAsync(
        PrioritizedSections sections,
        AnalysisTask task,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        // Add task-specific header
        sb.AppendLine($"# {task.Type} Analysis");
        sb.AppendLine();

        // Add high priority sections first
        foreach (var section in sections.HighPriority.OrderByDescending(s => s.Priority))
        {
            if (cancellationToken.IsCancellationRequested) break;

            sb.AppendLine($"## {section.Name}");
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        // Add medium priority sections if we have space
        foreach (var section in sections.MediumPriority.OrderByDescending(s => s.Priority))
        {
            if (cancellationToken.IsCancellationRequested) break;

            sb.AppendLine($"## {section.Name}");
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        // Add low priority sections (may be truncated later)
        foreach (var section in sections.LowPriority.OrderByDescending(s => s.Priority))
        {
            if (cancellationToken.IsCancellationRequested) break;

            sb.AppendLine($"## {section.Name}");
            sb.AppendLine(section.Content);
            sb.AppendLine();
        }

        return Task.FromResult(sb.ToString());
    }

    private int GetPriorityForContent(ContentSectionType type, AnalysisTaskType taskType)
    {
        return (type, taskType) switch
        {
            // Mods are always important for most tasks
            (ContentSectionType.ModList, AnalysisTaskType.Full) => 10,
            (ContentSectionType.ModList, AnalysisTaskType.Conflicts) => 10,
            (ContentSectionType.ModList, AnalysisTaskType.Performance) => 9,
            (ContentSectionType.ModList, _) => 8,

            // Configs are crucial for conflicts and performance
            (ContentSectionType.ConfigFile, AnalysisTaskType.Conflicts) => 10,
            (ContentSectionType.ConfigFile, AnalysisTaskType.Performance) => 9,
            (ContentSectionType.ConfigFile, AnalysisTaskType.Full) => 8,
            (ContentSectionType.ConfigFile, _) => 6,

            // Resource packs are lower priority unless specifically needed
            (ContentSectionType.ResourcePack, AnalysisTaskType.Full) => 5,
            (ContentSectionType.ResourcePack, _) => 3,

            // Summary is always high priority
            (ContentSectionType.Summary, _) => 10,

            _ => 5
        };
    }

    private void AddSectionToPriority(PrioritizedSections sections, ContentSection section)
    {
        if (section.Priority >= 8)
        {
            sections.HighPriority.Add(section);
        }
        else if (section.Priority >= 5)
        {
            sections.MediumPriority.Add(section);
        }
        else
        {
            sections.LowPriority.Add(section);
        }
    }

    private string GenerateModListContent(List<ModInfo> mods)
    {
        var sb = new StringBuilder();

        foreach (var mod in mods.Take(50)) // Limit for preprocessing
        {
            sb.AppendLine($"- **{mod.Name}** v{mod.Version} ({mod.ModId})");
            sb.AppendLine($"  File: {mod.FilePath}");
            sb.AppendLine($"  Size: {FormatFileSize(mod.FileSize)}");
        }

        if (mods.Count > 50)
        {
            sb.AppendLine($"... and {mods.Count - 50} more mods");
        }

        return sb.ToString();
    }

    private string GenerateConfigContent(IGrouping<string?, ConfigFile> configGroup)
    {
        var sb = new StringBuilder();

        foreach (var config in configGroup.Take(10)) // Limit per group
        {
            sb.AppendLine($"**{config.Name}** ({config.FileType})");
            if (!string.IsNullOrEmpty(config.Preview))
            {
                var preview = config.Preview.Length > 200
                    ? config.Preview.Substring(0, 200) + "..."
                    : config.Preview;
                sb.AppendLine($"```{config.Language}");
                sb.AppendLine(preview);
                sb.AppendLine("```");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GenerateResourcePackContent(List<ResourcePack> resourcePacks)
    {
        var sb = new StringBuilder();

        foreach (var pack in resourcePacks.Take(20))
        {
            sb.AppendLine($"- **{pack.Name}**");
            if (!string.IsNullOrEmpty(pack.Description))
            {
                sb.AppendLine($"  {pack.Description}");
            }
        }

        return sb.ToString();
    }

    private string GenerateSummaryContent(ScanResult scanResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**Total Mods:** {scanResult.Mods.Count}");
        sb.AppendLine($"**Config Files:** {scanResult.ConfigFiles.Count}");
        sb.AppendLine($"**Resource Packs:** {scanResult.ResourcePacks.Count}");
        sb.AppendLine($"**Total Files:** {scanResult.TotalFiles}");

        if (scanResult.Errors.Any())
        {
            sb.AppendLine($"**Errors:** {scanResult.Errors.Count}");
        }

        if (scanResult.Warnings.Any())
        {
            sb.AppendLine($"**Warnings:** {scanResult.Warnings.Count}");
        }

        return sb.ToString();
    }

    private int GetMaxTokensForTask(AnalysisTask task)
    {
        // Reserve some tokens for the response
        var maxTokens = _aiConfig.MaxTokens;

        return task.Type switch
        {
            AnalysisTaskType.Quick => (int)(maxTokens * 0.5),
            AnalysisTaskType.Summary => (int)(maxTokens * 0.6),
            AnalysisTaskType.Conflicts => (int)(maxTokens * 0.8),
            AnalysisTaskType.Performance => (int)(maxTokens * 0.8),
            AnalysisTaskType.Full => (int)(maxTokens * 0.9),
            _ => (int)(maxTokens * 0.7)
        };
    }

    private string TruncateLine(string line, int maxTokens)
    {
        if (EstimateTokenCount(line) <= maxTokens)
            return line;

        var targetLength = (int)(line.Length * ((double)maxTokens / EstimateTokenCount(line)));

        if (targetLength >= line.Length)
            return line;

        // Try to truncate at word boundary
        var truncated = line.Substring(0, Math.Max(1, targetLength));
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > targetLength * 0.8) // If we can find a good word boundary
        {
            truncated = truncated.Substring(0, lastSpace);
        }

        return truncated + "...";
    }

    private bool ShouldIncludeLine(string line, AnalysisTaskType taskType)
    {
        var lowerLine = line.ToLowerInvariant();

        // Always include headers and important markers
        if (lowerLine.StartsWith("#") || lowerLine.StartsWith("**") || lowerLine.StartsWith("- "))
            return true;

        // Task-specific filtering
        return taskType switch
        {
            AnalysisTaskType.Conflicts =>
                lowerLine.Contains("conflict") || lowerLine.Contains("error") ||
                lowerLine.Contains("duplicate") || lowerLine.Contains("incompatible"),

            AnalysisTaskType.Performance =>
                lowerLine.Contains("performance") || lowerLine.Contains("memory") ||
                lowerLine.Contains("cpu") || lowerLine.Contains("lag"),

            AnalysisTaskType.Quick =>
                !lowerLine.Contains("preview:") && line.Length < 200,

            _ => true // Include everything for full analysis
        };
    }

    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }
}
