using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for AI-powered analysis functionality
/// </summary>
public interface IAiAssistant
{
    /// <summary>
    /// Performs AI analysis on a scan result
    /// </summary>
    /// <param name="scanResult">The scan result to analyze</param>
    /// <param name="task">The analysis task to perform</param>
    /// <param name="modelOverride">Optional model override</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI analysis result</returns>
    Task<AiAnalysisResult> AnalyzeAsync(
        ScanResult scanResult, 
        AnalysisTask task, 
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs AI analysis with streaming output
    /// </summary>
    /// <param name="scanResult">The scan result to analyze</param>
    /// <param name="task">The analysis task to perform</param>
    /// <param name="onChunk">Callback for streaming chunks</param>
    /// <param name="modelOverride">Optional model override</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI analysis result</returns>
    Task<AiAnalysisResult> AnalyzeStreamingAsync(
        ScanResult scanResult,
        AnalysisTask task,
        Action<string> onChunk,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available AI models
    /// </summary>
    /// <returns>Dictionary of model names and their identifiers</returns>
    Task<Dictionary<string, string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the AI service is available
    /// </summary>
    /// <returns>True if AI service is available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recommended model for a specific task
    /// </summary>
    /// <param name="taskType">The task type</param>
    /// <returns>Recommended model identifier</returns>
    string GetRecommendedModel(AnalysisTaskType taskType);
}
