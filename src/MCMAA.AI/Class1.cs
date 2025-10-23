using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using MCMAA.Core.Configuration;
using MCMAA.Core.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCMAA.AI;

/// <summary>
/// Models for Ollama API communication
/// </summary>
public class OllamaRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 4096;
}

public class OllamaResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

public class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// AI Assistant implementation using Ollama
/// </summary>
public class OllamaAiAssistant : IAiAssistant
{
    private readonly ILogger<OllamaAiAssistant> _logger;
    private readonly AiConfiguration _aiConfig;
    private readonly TimeoutConfiguration _timeoutConfig;
    private readonly ICacheService _cacheService;
    private readonly ISessionManager _sessionManager;
    private readonly IContentPreprocessor _contentPreprocessor;
    private readonly IStreamingHandler _streamingHandler;

    // Cache for available models to avoid repeated API calls
    private Dictionary<string, string>? _cachedModels;
    private DateTime _modelsCacheExpiry = DateTime.MinValue;

    public OllamaAiAssistant(
        ILogger<OllamaAiAssistant> logger,
        IOptions<AiConfiguration> aiConfig,
        IOptions<TimeoutConfiguration> timeoutConfig,
        ICacheService cacheService,
        ISessionManager sessionManager,
        IContentPreprocessor contentPreprocessor,
        IStreamingHandler streamingHandler)
    {
        _logger = logger;
        _aiConfig = aiConfig.Value;
        _timeoutConfig = timeoutConfig.Value;
        _cacheService = cacheService;
        _sessionManager = sessionManager;
        _contentPreprocessor = contentPreprocessor;
        _streamingHandler = streamingHandler;
    }

    public string GetRecommendedModel(AnalysisTaskType taskType)
    {
        var taskName = taskType.ToString().ToLowerInvariant();
        if (_aiConfig.TaskModels.TryGetValue(taskName, out var modelKey) &&
            _aiConfig.Models.TryGetValue(modelKey, out var modelName))
        {
            return modelName;
        }

        // Fallback to standard model
        return _aiConfig.Models.GetValueOrDefault("standard", "qwen2.5-coder");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a temporary session to check availability
            var session = await _sessionManager.GetSessionAsync("phi3:mini", cancellationToken);
            try
            {
                if (session.HttpClient == null) return false;
                var response = await session.HttpClient.GetAsync("/api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            finally
            {
                await _sessionManager.ReleaseSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama service is not available");
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        // Return cached models if still valid (cache for 5 minutes)
        if (_cachedModels != null && DateTime.UtcNow < _modelsCacheExpiry)
        {
            return _cachedModels;
        }

        try
        {
            var session = await _sessionManager.GetSessionAsync("phi3:mini", cancellationToken);
            try
            {
                if (session.HttpClient == null) return new Dictionary<string, string>();

                var response = await session.HttpClient.GetAsync("/api/tags", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var modelsResponse = JsonSerializer.Deserialize<JsonElement>(content);

                var models = new Dictionary<string, string>();

                if (modelsResponse.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                models[name] = name;
                            }
                        }
                    }
                }

                // Cache the results
                _cachedModels = models;
                _modelsCacheExpiry = DateTime.UtcNow.AddMinutes(5);

                return models;
            }
            finally
            {
                await _sessionManager.ReleaseSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available models");
            return _cachedModels ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Warms up a model by sending a simple request to ensure it's loaded
    /// </summary>
    private async Task<bool> WarmupModelAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Warming up model {Model}", model);

            var session = await _sessionManager.GetSessionAsync(model, cancellationToken);
            try
            {
                if (session.HttpClient == null) return false;

                var warmupRequest = new OllamaRequest
                {
                    Model = model,
                    Prompt = "Hello", // Simple prompt for warmup
                    Stream = false,
                    Options = new OllamaOptions
                    {
                        Temperature = 0.1,
                        NumPredict = 1 // Minimal response
                    }
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Short timeout for warmup
                var requestJson = JsonSerializer.Serialize(warmupRequest);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await session.HttpClient.PostAsync("/api/generate", requestContent, cts.Token);
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger.LogDebug("Model {Model} warmed up successfully", model);
                }
                else
                {
                    _logger.LogDebug("Model {Model} warmup failed with status {StatusCode}", model, response.StatusCode);
                }

                return success;
            }
            finally
            {
                await _sessionManager.ReleaseSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Model {Model} warmup failed", model);
            return false;
        }
    }

    /// <summary>
    /// Validates if a model exists and returns a fallback if not
    /// </summary>
    private async Task<string> ValidateAndGetModelAsync(string requestedModel, CancellationToken cancellationToken = default)
    {
        var availableModels = await GetAvailableModelsAsync(cancellationToken);

        // If requested model exists, use it
        if (availableModels.ContainsKey(requestedModel))
        {
            return requestedModel;
        }

        _logger.LogWarning("Requested model '{RequestedModel}' not found. Available models: {AvailableModels}",
            requestedModel, string.Join(", ", availableModels.Keys));

        // Fallback logic: try to find a suitable alternative
        var fallbackModel = GetFallbackModel(requestedModel, availableModels.Keys.ToList());

        if (!string.IsNullOrEmpty(fallbackModel))
        {
            _logger.LogInformation("Using fallback model '{FallbackModel}' instead of '{RequestedModel}'",
                fallbackModel, requestedModel);

            // Try to warm up the fallback model
            await WarmupModelAsync(fallbackModel, cancellationToken);

            return fallbackModel;
        }

        // If no fallback found, throw an exception
        throw new InvalidOperationException($"Model '{requestedModel}' not found and no suitable fallback available. Available models: {string.Join(", ", availableModels.Keys)}");
    }

    /// <summary>
    /// Executes an operation with retry logic for transient failures
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (attempt <= maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(_timeoutConfig.BaseDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning("Attempt {Attempt} failed with transient error: {Error}. Retrying in {Delay}ms",
                    attempt, ex.Message, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // This should never be reached due to the loop logic, but satisfies the compiler
        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Determines if an error is transient and worth retrying
    /// </summary>
    private static bool IsTransientError(HttpRequestException ex)
    {
        return ex.Message.Contains("InternalServerError") ||
               ex.Message.Contains("BadGateway") ||
               ex.Message.Contains("ServiceUnavailable") ||
               ex.Message.Contains("GatewayTimeout");
    }

    /// <summary>
    /// Gets a fallback model based on the requested model characteristics
    /// </summary>
    private string? GetFallbackModel(string requestedModel, List<string> availableModels)
    {
        // If no models available, return null
        if (!availableModels.Any()) return null;

        // Fallback priority based on model characteristics
        var fallbackPriority = new[]
        {
            "phi3:mini",                           // Lightweight, reliable
            "phi3:3.8b-mini-4k-instruct-q4_0",   // More capable phi3
            "llama2:7b",                          // Common fallback
            "llama2:13b",                         // Larger fallback
            "mistral:7b",                         // Alternative architecture
        };

        // Try each fallback in order
        foreach (var fallback in fallbackPriority)
        {
            if (availableModels.Contains(fallback))
            {
                return fallback;
            }
        }

        // If no priority fallbacks found, return the first available model
        return availableModels.FirstOrDefault();
    }

    public async Task<AiAnalysisResult> AnalyzeAsync(
        ScanResult scanResult,
        AnalysisTask task,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Determine and validate model to use
        var requestedModel = modelOverride ?? GetRecommendedModel(task.Type);
        var model = await ValidateAndGetModelAsync(requestedModel, cancellationToken);

        _logger.LogInformation("Starting AI analysis with model {Model} for task {TaskType}", model, task.Type);

        var result = new AiAnalysisResult
        {
            Task = task,
            ModelUsed = model,
            AnalysisTimestamp = startTime,
            Temperature = _aiConfig.DefaultTemperature
        };

        try
        {
            // Preprocess content for optimal AI input
            var preprocessedContent = await _contentPreprocessor.PreprocessAsync(scanResult, task, cancellationToken);
            _logger.LogDebug("Content preprocessed: {OriginalTokens} -> {FinalTokens} tokens ({CompressionRatio:P1})",
                preprocessedContent.OriginalTokens, preprocessedContent.EstimatedTokens, preprocessedContent.CompressionRatio);

            // Generate prompt with preprocessed content
            var prompt = GeneratePromptWithPreprocessedContent(preprocessedContent, task);

            // Check cache first
            var cacheKey = _cacheService.GenerateKey(prompt, model, _aiConfig.DefaultTemperature, task.Type.ToString());
            var cachedResult = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);

            if (cachedResult != null)
            {
                _logger.LogDebug("Using cached result for analysis");
                result.Content = cachedResult;
                result.FromCache = true;
                result.CacheKey = cacheKey;
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }

            // Perform AI analysis
            var ollamaRequest = new OllamaRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = _aiConfig.DefaultTemperature,
                    NumPredict = _aiConfig.MaxTokens
                }
            };

            // Get session for this model
            var session = await _sessionManager.GetSessionAsync(model, cancellationToken);
            try
            {
                if (session.HttpClient == null)
                {
                    throw new InvalidOperationException("Session HTTP client is null");
                }

                var timeout = GetTimeoutForTask(task.TimeoutCategory);
                _logger.LogDebug("Using timeout of {Timeout} seconds for task category {Category}", timeout, task.TimeoutCategory);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeout));

                var requestJson = JsonSerializer.Serialize(ollamaRequest);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await session.HttpClient.PostAsync("/api/generate", requestContent, cts.Token);

                // Check status code and provide detailed error information
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);

                // Update session statistics
                session.RequestCount++;
                session.TotalDuration += DateTime.UtcNow - startTime;

                if (ollamaResponse != null)
                {
                    result.Content = ollamaResponse.Response;
                    result.TokensUsed = ollamaResponse.EvalCount + ollamaResponse.PromptEvalCount;

                    // Cache the result
                    await _cacheService.SetAsync(cacheKey, result.Content, cancellationToken: cancellationToken);
                    result.CacheKey = cacheKey;
                }
                else
                {
                    result.Success = false;
                    result.Errors.Add("Failed to parse Ollama response");
                }
            }
            finally
            {
                await _sessionManager.ReleaseSessionAsync(session, cancellationToken);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("llama runner process has terminated"))
        {
            _logger.LogWarning(ex, "Ollama process terminated, this may be due to resource constraints");
            result.Success = false;
            result.Errors.Add("AI service temporarily unavailable due to resource constraints. Try reducing the complexity of your request or try again later.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("InternalServerError"))
        {
            _logger.LogWarning(ex, "Ollama internal server error");
            result.Success = false;
            result.Errors.Add($"AI service error: {ex.Message}. This may be temporary - try again in a few moments.");
        }
        catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
        {
            _logger.LogWarning(ex, "Request timed out due to HttpClient timeout");
            result.Success = false;
            result.Errors.Add("Request timed out. The model may be loading or processing a complex request. Try again in a few moments or use a simpler task.");
        }
        catch (OperationCanceledException ex) when (ex.Message.Contains("timeout"))
        {
            _logger.LogWarning(ex, "Request timed out due to custom timeout");
            result.Success = false;
            result.Errors.Add("Request timed out. The model may be loading or processing a complex request. Try again in a few moments or use a simpler task.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Success = false;
            result.Errors.Add("Analysis was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI analysis");
            result.Success = false;
            result.Errors.Add($"Analysis failed: {ex.Message}");
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    public async Task<AiAnalysisResult> AnalyzeStreamingAsync(
        ScanResult scanResult,
        AnalysisTask task,
        Action<string> onChunk,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Determine and validate model to use
        var requestedModel = modelOverride ?? GetRecommendedModel(task.Type);
        var model = await ValidateAndGetModelAsync(requestedModel, cancellationToken);

        _logger.LogInformation("Starting streaming AI analysis with model {Model} for task {TaskType}", model, task.Type);

        var result = new AiAnalysisResult
        {
            Task = task,
            ModelUsed = model,
            AnalysisTimestamp = startTime,
            Temperature = _aiConfig.DefaultTemperature,
            StreamingUsed = true
        };

        try
        {
            // Preprocess content for optimal AI input
            var preprocessedContent = await _contentPreprocessor.PreprocessAsync(scanResult, task, cancellationToken);
            _logger.LogDebug("Content preprocessed for streaming: {OriginalTokens} -> {FinalTokens} tokens ({CompressionRatio:P1})",
                preprocessedContent.OriginalTokens, preprocessedContent.EstimatedTokens, preprocessedContent.CompressionRatio);

            // Generate prompt with preprocessed content
            var prompt = GeneratePromptWithPreprocessedContent(preprocessedContent, task);

            // Check cache first
            var cacheKey = _cacheService.GenerateKey(prompt, model, _aiConfig.DefaultTemperature, task.Type.ToString());
            var cachedResult = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);

            if (cachedResult != null)
            {
                _logger.LogDebug("Using cached result for streaming analysis");
                result.Content = cachedResult;
                result.FromCache = true;
                result.CacheKey = cacheKey;
                result.Duration = DateTime.UtcNow - startTime;

                // Simulate streaming for cached content using enhanced streaming
                var streamingOptions = new StreamingOptions
                {
                    ShowProgress = false,
                    OnChunk = onChunk
                };

                var chunks = cachedResult.ToChunkedStream(50, TimeSpan.FromMilliseconds(10));
                await _streamingHandler.ProcessStreamAsync(chunks, streamingOptions, cancellationToken);
                return result;
            }

            // Perform streaming AI analysis
            var ollamaRequest = new OllamaRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = true,
                Options = new OllamaOptions
                {
                    Temperature = _aiConfig.DefaultTemperature,
                    NumPredict = _aiConfig.MaxTokens
                }
            };

            // Get session for this model
            var session = await _sessionManager.GetSessionAsync(model, cancellationToken);
            try
            {
                if (session.HttpClient == null)
                {
                    throw new InvalidOperationException("Session HTTP client is null");
                }

                var timeout = GetTimeoutForTask(task.TimeoutCategory);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeout));

                var requestJson = JsonSerializer.Serialize(ollamaRequest);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var response = await session.HttpClient.PostAsync("/api/generate", requestContent, cts.Token);

                // For streaming requests, check status code manually to avoid premature connection termination
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

            var contentBuilder = new StringBuilder();
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null && !cts.Token.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var streamResponse = JsonSerializer.Deserialize<OllamaResponse>(line);
                    if (streamResponse != null && !string.IsNullOrEmpty(streamResponse.Response))
                    {
                        contentBuilder.Append(streamResponse.Response);
                        onChunk(streamResponse.Response);

                        if (streamResponse.Done)
                        {
                            result.TokensUsed = streamResponse.EvalCount + streamResponse.PromptEvalCount;
                            break;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse streaming response line: {Line}", line);
                }
            }

                result.Content = contentBuilder.ToString();

                // Update session statistics
                session.RequestCount++;
                session.TotalDuration += DateTime.UtcNow - startTime;

                // Cache the result
                if (!string.IsNullOrEmpty(result.Content))
                {
                    await _cacheService.SetAsync(cacheKey, result.Content, cancellationToken: cancellationToken);
                    result.CacheKey = cacheKey;
                }
            }
            finally
            {
                await _sessionManager.ReleaseSessionAsync(session, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Success = false;
            result.Errors.Add("Streaming analysis was cancelled");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("llama runner process has terminated"))
        {
            _logger.LogWarning(ex, "Ollama process terminated, this may be due to resource constraints");
            result.Success = false;
            result.Errors.Add("AI service temporarily unavailable due to resource constraints. Try reducing the complexity of your request or try again later.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("InternalServerError"))
        {
            _logger.LogWarning(ex, "Ollama internal server error");
            result.Success = false;
            result.Errors.Add($"AI service error: {ex.Message}. This may be temporary - try again in a few moments.");
        }
        catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
        {
            _logger.LogWarning(ex, "Streaming request timed out due to HttpClient timeout");
            result.Success = false;
            result.Errors.Add("Streaming request timed out. The model may be loading or processing a complex request. Try again in a few moments or use a simpler task.");
        }
        catch (OperationCanceledException ex) when (ex.Message.Contains("timeout"))
        {
            _logger.LogWarning(ex, "Streaming request timed out due to custom timeout");
            result.Success = false;
            result.Errors.Add("Streaming request timed out. The model may be loading or processing a complex request. Try again in a few moments or use a simpler task.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.Success = false;
            result.Errors.Add("Streaming analysis was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming AI analysis");
            result.Success = false;
            result.Errors.Add($"Streaming analysis failed: {ex.Message}");
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private string GeneratePrompt(ScanResult scanResult, AnalysisTask task)
    {
        var sb = new StringBuilder();

        // Task-specific prompt
        sb.AppendLine(GetTaskPrompt(task.Type));
        sb.AppendLine();

        // Modpack information
        sb.AppendLine("## Modpack Information");
        sb.AppendLine($"**Path:** {scanResult.ScanPath}");
        sb.AppendLine($"**Scan Date:** {scanResult.ScanTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Files:** {scanResult.TotalFiles}");
        sb.AppendLine($"**Mods:** {scanResult.Mods.Count}");
        sb.AppendLine($"**Config Files:** {scanResult.ConfigFiles.Count}");
        sb.AppendLine($"**Resource Packs:** {scanResult.ResourcePacks.Count}");
        sb.AppendLine();

        // Include the markdown report
        sb.AppendLine("## Detailed Scan Report");
        sb.AppendLine(scanResult.MarkdownReport);

        return sb.ToString();
    }

    private string GeneratePromptWithPreprocessedContent(PreprocessedContent preprocessedContent, AnalysisTask task)
    {
        var sb = new StringBuilder();

        // Task-specific prompt
        sb.AppendLine(GetTaskPrompt(task.Type));
        sb.AppendLine();

        // Add preprocessing metadata
        sb.AppendLine("## Content Processing Information");
        sb.AppendLine($"**Original Tokens:** {preprocessedContent.OriginalTokens}");
        sb.AppendLine($"**Optimized Tokens:** {preprocessedContent.EstimatedTokens}");
        sb.AppendLine($"**Compression Ratio:** {preprocessedContent.CompressionRatio:P1}");

        if (preprocessedContent.OptimizationSteps.Any())
        {
            sb.AppendLine($"**Optimization Steps:** {string.Join(", ", preprocessedContent.OptimizationSteps)}");
        }
        sb.AppendLine();

        // Add section priorities information
        if (preprocessedContent.Sections.TotalSections > 0)
        {
            sb.AppendLine("## Content Prioritization");
            sb.AppendLine($"**High Priority Sections:** {preprocessedContent.Sections.HighPriority.Count}");
            sb.AppendLine($"**Medium Priority Sections:** {preprocessedContent.Sections.MediumPriority.Count}");
            sb.AppendLine($"**Low Priority Sections:** {preprocessedContent.Sections.LowPriority.Count}");
            sb.AppendLine();
        }

        // Add the optimized content
        sb.AppendLine("## Optimized Modpack Analysis Content");
        sb.AppendLine(preprocessedContent.Content);

        return sb.ToString();
    }

    private string GetTaskPrompt(AnalysisTaskType taskType)
    {
        return taskType switch
        {
            AnalysisTaskType.Full => @"# Comprehensive Modpack Analysis

Please provide a comprehensive analysis of this Minecraft modpack including:

1. **Overview**: General assessment of the modpack's theme, complexity, and target audience
2. **Mod Analysis**: Key mods identified, their purposes, and how they work together
3. **Configuration Review**: Important configuration settings and their implications
4. **Performance Considerations**: Potential performance impacts and optimization suggestions
5. **Compatibility Assessment**: Potential mod conflicts or compatibility issues
6. **Recommendations**: Suggestions for improvements, additions, or modifications
7. **Resource Requirements**: Estimated system requirements and resource usage

Please be thorough and provide actionable insights.",

            AnalysisTaskType.Quick => @"# Quick Modpack Summary

Please provide a concise summary of this Minecraft modpack including:

1. **Theme/Focus**: What type of modpack this is (tech, magic, adventure, etc.)
2. **Key Mods**: 5-10 most important mods and their purposes
3. **Complexity Level**: Beginner, intermediate, or advanced
4. **Notable Features**: Unique or interesting aspects
5. **Quick Assessment**: Overall quality and potential issues

Keep the analysis brief but informative.",

            AnalysisTaskType.Summary => @"# Modpack Summary

Please provide a structured summary of this Minecraft modpack focusing on:

1. **Modpack Type**: Category and theme
2. **Major Mods**: Primary mods that define the pack
3. **Configuration Highlights**: Important config changes
4. **Target Audience**: Who this pack is designed for
5. **Overall Assessment**: Brief evaluation

Provide a clear, organized summary.",

            AnalysisTaskType.Conflicts => @"# Conflict Detection Analysis

Please analyze this Minecraft modpack specifically for potential conflicts and compatibility issues:

1. **Mod Conflicts**: Identify mods that might conflict with each other
2. **ID Conflicts**: Potential block/item ID conflicts
3. **Recipe Conflicts**: Overlapping or conflicting recipes
4. **Performance Conflicts**: Mods that might cause performance issues together
5. **Configuration Issues**: Config settings that might cause problems
6. **Version Compatibility**: Mods that might have version compatibility issues
7. **Recommendations**: How to resolve identified conflicts

Focus specifically on identifying and resolving potential issues.",

            AnalysisTaskType.Performance => @"# Performance Optimization Analysis

Please analyze this Minecraft modpack for performance optimization opportunities:

1. **Performance Impact**: Identify mods with high performance costs
2. **Configuration Optimization**: Config changes to improve performance
3. **Resource Usage**: Memory, CPU, and storage considerations
4. **Optimization Suggestions**: Specific recommendations to improve performance
5. **Alternative Mods**: Lighter alternatives to heavy mods
6. **System Requirements**: Recommended hardware specifications
7. **Troubleshooting**: Common performance issues and solutions

Focus on actionable performance improvements.",

            _ => "Please analyze this Minecraft modpack and provide insights about its structure, mods, and configuration."
        };
    }

    private int GetTimeoutForTask(TimeoutCategory category)
    {
        // Allow longer timeouts for model loading and processing
        return category switch
        {
            TimeoutCategory.Standard => _timeoutConfig.RequestStandard,    // 3 minutes
            TimeoutCategory.Large => _timeoutConfig.RequestLarge,          // 5 minutes
            TimeoutCategory.Complex => _timeoutConfig.RequestComplex,      // 8 minutes
            _ => _timeoutConfig.RequestStandard
        };
    }
}
