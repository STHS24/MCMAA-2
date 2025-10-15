using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for content preprocessing and optimization
/// </summary>
public interface IContentPreprocessor
{
    /// <summary>
    /// Preprocess scan result for optimal AI input
    /// </summary>
    Task<PreprocessedContent> PreprocessAsync(ScanResult scanResult, AnalysisTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimate token count for content
    /// </summary>
    int EstimateTokenCount(string content);

    /// <summary>
    /// Optimize content to fit within token limits
    /// </summary>
    Task<string> OptimizeContentAsync(string content, int maxTokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prioritize sections based on analysis task
    /// </summary>
    Task<PrioritizedSections> PrioritizeSectionsAsync(ScanResult scanResult, AnalysisTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Filter redundant or irrelevant content
    /// </summary>
    Task<string> FilterContentAsync(string content, AnalysisTask task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Preprocessed content with metadata
/// </summary>
public class PreprocessedContent
{
    public string Content { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
    public int OriginalTokens { get; set; }
    public double CompressionRatio { get; set; }
    public List<string> OptimizationSteps { get; set; } = new();
    public PrioritizedSections Sections { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Prioritized content sections
/// </summary>
public class PrioritizedSections
{
    public List<ContentSection> HighPriority { get; set; } = new();
    public List<ContentSection> MediumPriority { get; set; } = new();
    public List<ContentSection> LowPriority { get; set; } = new();
    public int TotalSections { get; set; }
    public Dictionary<string, int> SectionCounts { get; set; } = new();
}

/// <summary>
/// Content section with priority and metadata
/// </summary>
public class ContentSection
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ContentSectionType Type { get; set; }
    public int EstimatedTokens { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Types of content sections
/// </summary>
public enum ContentSectionType
{
    ModList,
    ConfigFile,
    ResourcePack,
    Summary,
    Metadata,
    Error,
    Warning
}
