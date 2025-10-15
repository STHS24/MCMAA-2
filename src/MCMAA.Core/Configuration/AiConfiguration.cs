namespace MCMAA.Core.Configuration;

/// <summary>
/// Configuration for AI models and task routing
/// </summary>
public class AiConfiguration
{
    /// <summary>
    /// Available AI models
    /// </summary>
    public Dictionary<string, string> Models { get; set; } = new()
    {
        ["lightweight"] = "phi3:mini",
        ["standard"] = "qwen2.5-coder",
        ["advanced"] = "qwen2.5:14b"
    };

    /// <summary>
    /// Task to model mapping
    /// </summary>
    public Dictionary<string, string> TaskModels { get; set; } = new()
    {
        ["summary"] = "lightweight",
        ["quick"] = "lightweight",
        ["conflicts"] = "standard",
        ["performance"] = "standard",
        ["full"] = "standard"
    };

    /// <summary>
    /// Ollama server base URL
    /// </summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default temperature for AI requests
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.7;

    /// <summary>
    /// Maximum tokens for AI requests
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Enable streaming responses
    /// </summary>
    public bool EnableStreaming { get; set; } = true;
}
