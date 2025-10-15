namespace MCMAA.Core.Models;

/// <summary>
/// Types of analysis tasks that can be performed
/// </summary>
public enum AnalysisTaskType
{
    /// <summary>
    /// Full comprehensive analysis
    /// </summary>
    Full,

    /// <summary>
    /// Quick summary analysis
    /// </summary>
    Quick,

    /// <summary>
    /// Summary only
    /// </summary>
    Summary,

    /// <summary>
    /// Conflict detection analysis
    /// </summary>
    Conflicts,

    /// <summary>
    /// Performance optimization analysis
    /// </summary>
    Performance
}

/// <summary>
/// Analysis task configuration
/// </summary>
public class AnalysisTask
{
    /// <summary>
    /// Task type
    /// </summary>
    public AnalysisTaskType Type { get; set; }

    /// <summary>
    /// Task name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Task description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Recommended model for this task
    /// </summary>
    public string RecommendedModel { get; set; } = string.Empty;

    /// <summary>
    /// Expected timeout category
    /// </summary>
    public TimeoutCategory TimeoutCategory { get; set; } = TimeoutCategory.Standard;

    /// <summary>
    /// Priority level (1-10, higher is more important)
    /// </summary>
    public int Priority { get; set; } = 5;
}

/// <summary>
/// Timeout categories for different types of requests
/// </summary>
public enum TimeoutCategory
{
    Standard,
    Large,
    Complex
}
