using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace OllamaTestScript
{
    /// <summary>
    /// Standalone test script for Ollama API testing outside of MCMAA configuration
    /// </summary>
    public class OllamaTestScript : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public OllamaTestScript(string baseUrl = "http://localhost:11434", bool lowSpecMode = false)
        {
            _baseUrl = baseUrl;
            
            // Set Ollama performance optimization environment variables
            SetOllamaOptimizationEnvironment(lowSpecMode);
            
            // Use optimized HttpClient configuration
            var httpClientHandler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 10
            };
            
            _httpClient = new HttpClient(httpClientHandler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromMinutes(10)
            };
            
            // Add performance headers
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Keep-Alive", "timeout=60, max=100");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false, // Disable indentation for better performance
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Ollama API Models

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

            [JsonPropertyName("num_ctx")]
            public int? NumCtx { get; set; }

            [JsonPropertyName("num_batch")]
            public int? NumBatch { get; set; }

            [JsonPropertyName("num_thread")]
            public int? NumThread { get; set; }

            [JsonPropertyName("top_p")]
            public double? TopP { get; set; }

            [JsonPropertyName("top_k")]
            public int? TopK { get; set; }

            [JsonPropertyName("repeat_penalty")]
            public double? RepeatPenalty { get; set; }

            [JsonPropertyName("stop")]
            public string[]? Stop { get; set; }
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

            [JsonPropertyName("eval_duration")]
            public long EvalDuration { get; set; }

            [JsonPropertyName("context")]
            public int[]? Context { get; set; }
        }

        public class OllamaModelInfo
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("modified_at")]
            public DateTime ModifiedAt { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("digest")]
            public string Digest { get; set; } = string.Empty;

            [JsonPropertyName("details")]
            public OllamaModelDetails? Details { get; set; }
        }

        public class OllamaModelDetails
        {
            [JsonPropertyName("parent_model")]
            public string? ParentModel { get; set; }

            [JsonPropertyName("format")]
            public string? Format { get; set; }

            [JsonPropertyName("family")]
            public string? Family { get; set; }

            [JsonPropertyName("families")]
            public string[]? Families { get; set; }

            [JsonPropertyName("parameter_size")]
            public string? ParameterSize { get; set; }

            [JsonPropertyName("quantization_level")]
            public string? QuantizationLevel { get; set; }
        }

        /// <summary>
        /// Set Ollama server optimization environment variables
        /// </summary>
        private void SetOllamaOptimizationEnvironment(bool lowSpecMode = false)
        {
            Console.WriteLine("🔧 Setting Ollama performance optimizations...");
            
            // Core performance settings
            Environment.SetEnvironmentVariable("OLLAMA_MAX_LOADED_MODELS", "1");
            Environment.SetEnvironmentVariable("OLLAMA_NUM_PARALLEL", "1");
            Environment.SetEnvironmentVariable("OLLAMA_FLASH_ATTENTION", "1");
            Environment.SetEnvironmentVariable("OLLAMA_GPU_MEMORY_FRACTION", "0.9");
            
            // Additional optimizations
            var keepAlive = Environment.GetEnvironmentVariable("OLLAMA_KEEP_ALIVE") ?? "30m";
            Environment.SetEnvironmentVariable("OLLAMA_KEEP_ALIVE", keepAlive);
            Environment.SetEnvironmentVariable("OLLAMA_HOST", "0.0.0.0:11434");
            
            // Advanced GPU optimization settings
            Environment.SetEnvironmentVariable("OLLAMA_NUM_GPU", Environment.GetEnvironmentVariable("OLLAMA_NUM_GPU") ?? "1");
            Environment.SetEnvironmentVariable("OLLAMA_NUM_THREAD", Environment.GetEnvironmentVariable("OLLAMA_NUM_THREAD") ?? "8");
            Environment.SetEnvironmentVariable("OLLAMA_BATCH_SIZE", Environment.GetEnvironmentVariable("OLLAMA_BATCH_SIZE") ?? "512");
            Environment.SetEnvironmentVariable("OLLAMA_CONTEXT_SIZE", Environment.GetEnvironmentVariable("OLLAMA_CONTEXT_SIZE") ?? "4096");
            
            // Low-spec system optimizations
            if (lowSpecMode)
            {
                Console.WriteLine("🔧 Applying low-spec system optimizations...");
                Environment.SetEnvironmentVariable("OLLAMA_NUM_GPU", "0");  // Force CPU-only mode
                Environment.SetEnvironmentVariable("OLLAMA_NUM_THREAD", "4");  // Conservative threading
                Environment.SetEnvironmentVariable("OLLAMA_GPU_MEMORY_FRACTION", "0.0");  // No GPU memory
                Environment.SetEnvironmentVariable("OLLAMA_BATCH_SIZE", "128");  // Smaller batches
                Environment.SetEnvironmentVariable("OLLAMA_CONTEXT_SIZE", "2048");  // Smaller context window
                Environment.SetEnvironmentVariable("OLLAMA_MAX_LOADED_MODELS", "1");  // Single model only
                Console.WriteLine("✅ Low-spec optimizations applied (CPU-only mode)");
            }
            
            Console.WriteLine("✅ Ollama optimizations applied:");
            Console.WriteLine("   • OLLAMA_MAX_LOADED_MODELS=1 (keep only 1 model in memory)");
            Console.WriteLine("   • OLLAMA_NUM_PARALLEL=1 (single request processing)");
            Console.WriteLine("   • OLLAMA_FLASH_ATTENTION=1 (optimized attention mechanism)");
            Console.WriteLine("   • OLLAMA_GPU_MEMORY_FRACTION=0.9 (use 90% of GPU memory)");
            Console.WriteLine($"   • OLLAMA_KEEP_ALIVE={keepAlive} (keep model loaded for {keepAlive})");
            Console.WriteLine($"   • OLLAMA_NUM_GPU={Environment.GetEnvironmentVariable("OLLAMA_NUM_GPU")} (GPU layers)");
            Console.WriteLine($"   • OLLAMA_NUM_THREAD={Environment.GetEnvironmentVariable("OLLAMA_NUM_THREAD")} (CPU threads)");
            Console.WriteLine($"   • OLLAMA_BATCH_SIZE={Environment.GetEnvironmentVariable("OLLAMA_BATCH_SIZE")} (batch size)");
            Console.WriteLine($"   • OLLAMA_CONTEXT_SIZE={Environment.GetEnvironmentVariable("OLLAMA_CONTEXT_SIZE")} (context window)");
            Console.WriteLine();
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Performance metrics tracking
        /// </summary>
        public class PerformanceMetrics
        {
            public TimeSpan TotalDuration { get; set; }
            public TimeSpan NetworkDuration { get; set; }
            public TimeSpan SerializationDuration { get; set; }
            public TimeSpan DeserializationDuration { get; set; }
            public long BytesTransferred { get; set; }
            public int TokensGenerated { get; set; }
            public double TokensPerSecond => TokensGenerated / TotalDuration.TotalSeconds;
            public double NetworkThroughput => BytesTransferred / NetworkDuration.TotalSeconds;
        }

        /// <summary>
        /// Track performance metrics for a request with detailed debugging
        /// </summary>
        private async Task<T> MeasurePerformanceAsync<T>(string operationName, Func<Task<T>> operation)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;
            
            Console.WriteLine($"🔍 DEBUG: Starting '{operationName}' at {startTime:HH:mm:ss.fff}");
            
            try
            {
                var result = await operation();
                stopwatch.Stop();
                
                Console.WriteLine($"✅ DEBUG: '{operationName}' completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"❌ DEBUG: '{operationName}' failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Enhanced debugging for HTTP operations
        /// </summary>
        private void LogDebugInfo(string phase, TimeSpan duration, string? additionalInfo = null)
        {
            var debugEnabled = Environment.GetEnvironmentVariable("DEBUG_METRICS")?.ToLower() == "true";
            if (debugEnabled)
            {
                var info = additionalInfo != null ? $" - {additionalInfo}" : "";
                Console.WriteLine($"🔍 DEBUG: {phase}: {duration.TotalMilliseconds:F0}ms{info}");
            }
        }

        /// <summary>
        /// Check system resources and Ollama processes
        /// </summary>
        private void CheckSystemResources()
        {
            Console.WriteLine("🔍 DEBUG: Checking system resources...");
            
            try
            {
                // Check available memory
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64 / (1024 * 1024); // MB
                Console.WriteLine($"🔍 DEBUG: Current process memory: {workingSet} MB");
                
                // Check for Ollama processes
                var ollamaProcesses = System.Diagnostics.Process.GetProcessesByName("ollama");
                Console.WriteLine($"🔍 DEBUG: Found {ollamaProcesses.Length} Ollama processes");
                
                foreach (var proc in ollamaProcesses)
                {
                    try
                    {
                        var memoryMB = proc.WorkingSet64 / (1024 * 1024);
                        Console.WriteLine($"🔍 DEBUG: Ollama PID {proc.Id}: {memoryMB} MB memory");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"🔍 DEBUG: Could not get memory info for PID {proc.Id}: {ex.Message}");
                    }
                }
                
                // Check CPU load (basic)
                var cpuUsage = GetCpuUsage();
                Console.WriteLine($"🔍 DEBUG: CPU usage: {cpuUsage:F1}%");
                
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔍 DEBUG: Error checking system resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Get basic CPU usage (simplified)
        /// </summary>
        private double GetCpuUsage()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
                
                System.Threading.Thread.Sleep(100); // Wait 100ms
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                return cpuUsageTotal * 100;
            }
            catch
            {
                return 0.0;
            }
        }

        #endregion

        #region Core API Methods

        /// <summary>
        /// Check if Ollama service is available with debugging
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                Console.WriteLine($"🔍 DEBUG: Checking availability at {_baseUrl}/api/tags");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("/api/tags");
                stopwatch.Stop();
                LogDebugInfo("Availability Check", stopwatch.Elapsed, $"Status: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DEBUG: Availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of available models with debugging
        /// </summary>
        public async Task<List<OllamaModelInfo>> GetAvailableModelsAsync()
        {
            try
            {
                Console.WriteLine($"🔍 DEBUG: Getting models from {_baseUrl}/api/tags");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("/api/tags");
                stopwatch.Stop();
                LogDebugInfo("Models API Call", stopwatch.Elapsed, $"Status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ DEBUG: Models API error: {errorContent}");
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

                var contentStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var content = await response.Content.ReadAsStringAsync();
                contentStopwatch.Stop();
                LogDebugInfo("Content Reading", contentStopwatch.Elapsed, $"Size: {content.Length} bytes");

                var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var modelsResponse = JsonSerializer.Deserialize<JsonElement>(content);
                parseStopwatch.Stop();
                LogDebugInfo("JSON Parsing", parseStopwatch.Elapsed);

                var models = new List<OllamaModelInfo>();

                if (modelsResponse.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        var modelInfo = JsonSerializer.Deserialize<OllamaModelInfo>(model.GetRawText(), _jsonOptions);
                        if (modelInfo != null)
                        {
                            models.Add(modelInfo);
                        }
                    }
                }

                Console.WriteLine($"🔍 DEBUG: Parsed {models.Count} models");
                return models;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DEBUG: Error getting available models: {ex.Message}");
                Console.WriteLine($"❌ DEBUG: Stack trace: {ex.StackTrace}");
                return new List<OllamaModelInfo>();
            }
        }

        /// <summary>
        /// Send a simple request to Ollama with performance tracking and detailed debugging
        /// </summary>
        public async Task<(OllamaResponse? Response, PerformanceMetrics Metrics)> SendRequestWithMetricsAsync(
            string model, string prompt, OllamaOptions? options = null, bool stream = false)
        {
            var metrics = new PerformanceMetrics();
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Console.WriteLine($"🔍 DEBUG: Starting request to model '{model}'");
            Console.WriteLine($"🔍 DEBUG: Prompt length: {prompt.Length} characters");
            Console.WriteLine($"🔍 DEBUG: Options - Temperature: {options?.Temperature ?? 0.7}, MaxTokens: {options?.NumPredict ?? 4096}");
            
            try
            {
                // Phase 1: Request Preparation
                var prepStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var request = new OllamaRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Stream = stream,
                    Options = options ?? new OllamaOptions()
                };
                prepStopwatch.Stop();
                LogDebugInfo("Request Preparation", prepStopwatch.Elapsed);

                // Phase 2: JSON Serialization
                var serializationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                serializationStopwatch.Stop();
                metrics.SerializationDuration = serializationStopwatch.Elapsed;
                LogDebugInfo("JSON Serialization", serializationStopwatch.Elapsed, $"Size: {requestJson.Length} bytes");

                // Phase 3: HTTP Content Creation
                var contentStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                contentStopwatch.Stop();
                LogDebugInfo("HTTP Content Creation", contentStopwatch.Elapsed);

                Console.WriteLine($"🚀 Sending request to model '{model}'...");
                Console.WriteLine($"📝 Prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}{(prompt.Length > 100 ? "..." : "")}");
                Console.WriteLine($"⚙️ Options: Temperature={request.Options.Temperature}, MaxTokens={request.Options.NumPredict}");
                Console.WriteLine();

                // Phase 4: Network Request
                var networkStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Console.WriteLine($"🔍 DEBUG: Sending HTTP POST to {_baseUrl}/api/generate");
                var response = await _httpClient.PostAsync("/api/generate", requestContent);
                networkStopwatch.Stop();
                metrics.NetworkDuration = networkStopwatch.Elapsed;
                LogDebugInfo("Network Request", networkStopwatch.Elapsed, $"Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ DEBUG: HTTP Error - Status: {response.StatusCode}");
                    Console.WriteLine($"❌ DEBUG: Error Content: {errorContent}");
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

                // Phase 5: Response Reading
                var responseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var responseContent = await response.Content.ReadAsStringAsync();
                responseStopwatch.Stop();
                metrics.BytesTransferred = responseContent.Length;
                LogDebugInfo("Response Reading", responseStopwatch.Elapsed, $"Size: {responseContent.Length} bytes");

                // Phase 6: JSON Deserialization
                var deserializationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, _jsonOptions);
                deserializationStopwatch.Stop();
                metrics.DeserializationDuration = deserializationStopwatch.Elapsed;
                LogDebugInfo("JSON Deserialization", deserializationStopwatch.Elapsed);

                if (ollamaResponse != null)
                {
                    metrics.TokensGenerated = ollamaResponse.EvalCount + ollamaResponse.PromptEvalCount;
                    Console.WriteLine($"🔍 DEBUG: Response parsed - Tokens: {metrics.TokensGenerated}");
                }

                totalStopwatch.Stop();
                metrics.TotalDuration = totalStopwatch.Elapsed;

                Console.WriteLine($"🔍 DEBUG: Total request completed in {totalStopwatch.ElapsedMilliseconds}ms");
                return (ollamaResponse, metrics);
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                metrics.TotalDuration = totalStopwatch.Elapsed;
                Console.WriteLine($"❌ DEBUG: Request failed after {totalStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"❌ DEBUG: Error: {ex.Message}");
                Console.WriteLine($"❌ DEBUG: Stack Trace: {ex.StackTrace}");
                return (null, metrics);
            }
        }

        /// <summary>
        /// Send a simple request to Ollama (backward compatibility)
        /// </summary>
        public async Task<OllamaResponse?> SendRequestAsync(string model, string prompt, OllamaOptions? options = null, bool stream = false)
        {
            var (response, _) = await SendRequestWithMetricsAsync(model, prompt, options, stream);
            return response;
        }

        /// <summary>
        /// Send a streaming request to Ollama
        /// </summary>
        public async Task<string> SendStreamingRequestAsync(string model, string prompt, OllamaOptions? options = null, Action<string>? onChunk = null)
        {
            try
            {
                var request = new OllamaRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Stream = true,
                    Options = options ?? new OllamaOptions()
                };

                var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                Console.WriteLine($"🚀 Sending streaming request to model '{model}'...");
                Console.WriteLine($"📝 Prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}{(prompt.Length > 100 ? "..." : "")}");
                Console.WriteLine($"⚙️ Options: Temperature={request.Options.Temperature}, MaxTokens={request.Options.NumPredict}");
                Console.WriteLine();

                var response = await _httpClient.PostAsync("/api/generate", requestContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
                }

                var contentBuilder = new StringBuilder();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var streamResponse = JsonSerializer.Deserialize<OllamaResponse>(line, _jsonOptions);
                        if (streamResponse != null && !string.IsNullOrEmpty(streamResponse.Response))
                        {
                            contentBuilder.Append(streamResponse.Response);
                            onChunk?.Invoke(streamResponse.Response);

                            if (streamResponse.Done)
                            {
                                Console.WriteLine($"\n✅ Streaming completed. Tokens: {streamResponse.EvalCount + streamResponse.PromptEvalCount}");
                                break;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"⚠️ Failed to parse streaming response line: {ex.Message}");
                    }
                }

                return contentBuilder.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending streaming request: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Warm up a model with a simple request
        /// </summary>
        public async Task<bool> WarmupModelAsync(string model)
        {
            try
            {
                Console.WriteLine($"🔥 Warming up model '{model}'...");
                
                var warmupOptions = new OllamaOptions
                {
                    Temperature = 0.1,
                    NumPredict = 1
                };

                var response = await SendRequestAsync(model, "Hello", warmupOptions);
                
                if (response != null)
                {
                    Console.WriteLine($"✅ Model '{model}' warmed up successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ Failed to warm up model '{model}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error warming up model '{model}': {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Test Scenarios

        /// <summary>
        /// Test basic functionality with detailed debugging
        /// </summary>
        public async Task RunBasicTestAsync(string model = "codellama:7b")
        {
            var testStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("🧪 Running Basic Ollama Test");
            Console.WriteLine("═══════════════════════════════");
            Console.WriteLine($"🔍 DEBUG: Test started at {DateTime.Now:HH:mm:ss.fff}");
            
            // Check system resources
            CheckSystemResources();

            // Check availability
            Console.WriteLine("1. Checking Ollama availability...");
            var availabilityStopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (!await IsAvailableAsync())
            {
                Console.WriteLine("❌ Ollama service is not available. Please ensure Ollama is running.");
                return;
            }
            availabilityStopwatch.Stop();
            Console.WriteLine($"✅ Ollama service is available ({availabilityStopwatch.ElapsedMilliseconds}ms)");

            // Get available models
            Console.WriteLine("\n2. Getting available models...");
            var modelsStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var models = await GetAvailableModelsAsync();
            modelsStopwatch.Stop();
            Console.WriteLine($"🔍 DEBUG: Model discovery took {modelsStopwatch.ElapsedMilliseconds}ms");
            
            if (!models.Any())
            {
                Console.WriteLine("❌ No models available. Please install some models first.");
                return;
            }

            Console.WriteLine($"✅ Found {models.Count} models:");
            foreach (var availableModel in models.Take(5)) // Show first 5 models
            {
                var sizeInGB = availableModel.Size / (1024.0 * 1024.0 * 1024.0);
                Console.WriteLine($"   • {availableModel.Name} ({sizeInGB:F2} GB)");
            }

            // Check if the specified model exists, fallback to first available if not
            var testModel = models.FirstOrDefault(m => m.Name == model)?.Name;
            if (testModel == null)
            {
                testModel = models.First().Name;
                Console.WriteLine($"\n⚠️ Model '{model}' not found, using first available model: '{testModel}'");
            }
            else
            {
                Console.WriteLine($"\n3. Testing with model '{testModel}'...");
            }

            var testPrompt = "Analyze this C# code and explain what it does:\n\npublic class ExampleClass\n{\n    private string _name;\n    \n    public ExampleClass(string name)\n    {\n        _name = name;\n    }\n    \n    public string GetName() => _name;\n    \n    public void SetName(string name)\n    {\n        _name = name;\n    }\n}";
            var testOptions = new OllamaOptions
            {
                Temperature = 0.7,
                NumPredict = 300,
                // Performance optimization parameters
                NumCtx = 2048,        // Context window size
                NumBatch = 8,         // Batch size for processing
                NumThread = 4,        // Number of threads
                RepeatPenalty = 1.1,  // Repetition penalty
                TopK = 40,           // Top-k sampling
                TopP = 0.9           // Top-p sampling
            };

            Console.WriteLine($"🔍 DEBUG: Starting main request at {DateTime.Now:HH:mm:ss.fff}");
            var result = await SendRequestWithMetricsAsync(testModel, testPrompt, testOptions);
            
            testStopwatch.Stop();
            Console.WriteLine($"🔍 DEBUG: Total test completed in {testStopwatch.ElapsedMilliseconds}ms");
            
            if (result.Response != null)
            {
                Console.WriteLine($"✅ Test successful!");
                Console.WriteLine($"📊 Response: {result.Response.Response}");
                Console.WriteLine($"📈 Tokens used: {result.Response.EvalCount + result.Response.PromptEvalCount}");
                
                // Display comprehensive performance metrics
                DisplayPerformanceMetrics(result.Response, result.Metrics);
            }
            else
            {
                Console.WriteLine("❌ Test failed");
                if (result.Metrics != null)
                {
                    Console.WriteLine($"⏱️ Request failed after: {result.Metrics.TotalDuration.TotalSeconds:F2}s");
                }
            }
        }

        /// <summary>
        /// Test streaming functionality
        /// </summary>
        public async Task RunStreamingTestAsync(string model)
        {
            Console.WriteLine($"🧪 Running Streaming Test with '{model}'");
            Console.WriteLine("═══════════════════════════════════════════");

            var testPrompt = "Write a short story about a Minecraft adventure.";
            var testOptions = new OllamaOptions
            {
                Temperature = 0.8,
                NumPredict = 500
            };

            Console.WriteLine("📡 Starting streaming response...");
            Console.WriteLine("─".PadRight(50, '─'));

            var response = await SendStreamingRequestAsync(model, testPrompt, testOptions, chunk =>
            {
                Console.Write(chunk);
            });

            Console.WriteLine("\n─".PadRight(50, '─'));
            Console.WriteLine($"✅ Streaming completed. Response length: {response.Length} characters");
            
            // Note: For streaming, we'd need to track timing separately
            // This is a simplified version - in a real implementation,
            // you'd track start time, first token time, and end time
        }

        /// <summary>
        /// Test Minecraft-specific prompts
        /// </summary>
        public async Task RunMinecraftTestAsync(string model)
        {
            Console.WriteLine($"🧪 Running Minecraft-Specific Test with '{model}'");
            Console.WriteLine("═══════════════════════════════════════════════════");

            var minecraftPrompts = new[]
            {
                "What are the main types of Minecraft mods?",
                "Explain the difference between Forge and Fabric mod loaders.",
                "What are some popular Minecraft modpacks and their themes?",
                "How do you optimize Minecraft performance with mods?",
                "What are common mod conflicts in Minecraft modpacks?"
            };

            foreach (var prompt in minecraftPrompts)
            {
                Console.WriteLine($"\n📝 Prompt: {prompt}");
                Console.WriteLine("─".PadRight(60, '─'));

                var options = new OllamaOptions
                {
                    Temperature = 0.7,
                    NumPredict = 300
                };

                var result = await SendRequestAsync(model, prompt, options);
                if (result != null)
                {
                    Console.WriteLine($"🤖 Response: {result.Response}");
                    DisplayPerformanceMetrics(result);
                }
                else
                {
                    Console.WriteLine("❌ Failed to get response");
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Test different temperature settings
        /// </summary>
        public async Task RunTemperatureTestAsync(string model)
        {
            Console.WriteLine($"🧪 Running Temperature Test with '{model}'");
            Console.WriteLine("═══════════════════════════════════════════════");

            var prompt = "Write a creative short story about a robot.";
            var temperatures = new[] { 0.1, 0.5, 0.7, 1.0, 1.2 };

            foreach (var temp in temperatures)
            {
                Console.WriteLine($"\n🌡️ Temperature: {temp}");
                Console.WriteLine("─".PadRight(40, '─'));

                var options = new OllamaOptions
                {
                    Temperature = temp,
                    NumPredict = 200
                };

                var result = await SendRequestAsync(model, prompt, options);
                if (result != null)
                {
                    Console.WriteLine($"📝 Response: {result.Response}");
                }
                else
                {
                    Console.WriteLine("❌ Failed to get response");
                }

                Console.WriteLine();
            }
        }

        #endregion

        #region Performance Metrics

        /// <summary>
        /// Display comprehensive performance metrics with detailed breakdown
        /// </summary>
        private void DisplayPerformanceMetrics(OllamaResponse response, PerformanceMetrics? metrics = null)
        {
            Console.WriteLine();
            Console.WriteLine("📊 Performance Metrics:");
            
            // Debug output if enabled
            if (Environment.GetEnvironmentVariable("DEBUG_METRICS")?.ToLower() == "true")
            {
                Console.WriteLine("🔍 DEBUG - Raw Response Metrics:");
                Console.WriteLine($"   • Load Duration (ns): {response.LoadDuration}");
                Console.WriteLine($"   • Eval Duration (ns): {response.EvalDuration}");
                Console.WriteLine($"   • Total Duration (ns): {response.TotalDuration}");
                Console.WriteLine($"   • Eval Count: {response.EvalCount}");
                Console.WriteLine($"   • Prompt Eval Count: {response.PromptEvalCount}");
                Console.WriteLine();
            }
            
            // Total duration (already in nanoseconds from Ollama)
            var totalSeconds = response.TotalDuration / 1_000_000_000.0;
            Console.WriteLine($"   • Total Duration: {totalSeconds:F2}s");
            
            // Model load time
            if (response.LoadDuration > 0)
            {
                var loadSeconds = response.LoadDuration / 1_000_000_000.0;
                Console.WriteLine($"   • Model Load Time: {loadSeconds:F2}s");
            }
            else
            {
                Console.WriteLine($"   • Model Load Time: N/A (model already loaded)");
            }
            
            // Generation time
            if (response.EvalDuration > 0)
            {
                var evalSeconds = response.EvalDuration / 1_000_000_000.0;
                Console.WriteLine($"   • Generation Time: {evalSeconds:F2}s");
                
                // Tokens per second calculation
                var totalTokens = response.EvalCount + response.PromptEvalCount;
                if (totalTokens > 0)
                {
                    var tokensPerSecond = totalTokens / evalSeconds;
                    Console.WriteLine($"   • Tokens/Second: {tokensPerSecond:F2}");
                }
                else
                {
                    Console.WriteLine($"   • Tokens/Second: N/A (no tokens generated)");
                }
            }
            else
            {
                Console.WriteLine($"   • Generation Time: N/A");
                Console.WriteLine($"   • Tokens/Second: N/A (insufficient data)");
            }
            
            // Token counts
            Console.WriteLine($"   • Tokens Used: {response.EvalCount + response.PromptEvalCount}");
            Console.WriteLine($"   • Input Tokens: {response.PromptEvalCount}");
            Console.WriteLine($"   • Output Tokens: {response.EvalCount}");
            
            // Enhanced metrics if available
            if (metrics != null)
            {
                Console.WriteLine();
                Console.WriteLine("🔧 Enhanced Performance Metrics:");
                Console.WriteLine($"   • Network Duration: {metrics.NetworkDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   • Serialization Time: {metrics.SerializationDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   • Deserialization Time: {metrics.DeserializationDuration.TotalMilliseconds:F0}ms");
                Console.WriteLine($"   • Bytes Transferred: {metrics.BytesTransferred:N0}");
                Console.WriteLine($"   • Network Throughput: {metrics.NetworkThroughput / 1024:F1} KB/s");
                
                if (metrics.TokensGenerated > 0)
                {
                    Console.WriteLine($"   • Overall Tokens/Second: {metrics.TokensPerSecond:F2}");
                }
            }
        }

        /// <summary>
        /// Display streaming performance metrics
        /// </summary>
        private void DisplayStreamingMetrics(TimeSpan totalDuration, TimeSpan timeToFirstToken, 
            TimeSpan generationDuration, int totalTokens)
        {
            Console.WriteLine();
            Console.WriteLine("📊 Performance Metrics:");
            Console.WriteLine($"   • Total Duration: {totalDuration.TotalSeconds:F2}s");
            Console.WriteLine($"   • Time to First Token: {timeToFirstToken.TotalSeconds:F2}s");
            Console.WriteLine($"   • Generation Duration: {generationDuration.TotalSeconds:F2}s");
            Console.WriteLine($"   • Total Tokens: {totalTokens}");
            
            if (generationDuration.TotalSeconds > 0 && totalTokens > 0)
            {
                var tokensPerSecond = totalTokens / generationDuration.TotalSeconds;
                Console.WriteLine($"   • Tokens/Second: {tokensPerSecond:F2}");
            }
            else
            {
                Console.WriteLine($"   • Tokens/Second: N/A (insufficient data)");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Format bytes to human readable string
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Save response to file
        /// </summary>
        public async Task SaveResponseToFileAsync(string content, string filename)
        {
            try
            {
                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
                Directory.CreateDirectory(outputDir);
                
                var filePath = Path.Combine(outputDir, filename);
                await File.WriteAllTextAsync(filePath, content);
                
                Console.WriteLine($"💾 Response saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving file: {ex.Message}");
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
        }
    }

    /// <summary>
    /// Main program entry point
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🤖 Ollama Test Script");
            Console.WriteLine("═══════════════════════");
            Console.WriteLine();

            // Parse command line arguments
            var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? 
                         Environment.GetEnvironmentVariable("AI__OllamaBaseUrl") ?? 
                         "http://localhost:11434";
            var model = Environment.GetEnvironmentVariable("MODEL") ?? "codellama:7b";
            var testType = Environment.GetEnvironmentVariable("TEST_TYPE") ?? "basic";
            var preloadModel = false;
            var lowSpecMode = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--url" when i + 1 < args.Length:
                        baseUrl = args[++i];
                        break;
                    case "--model" when i + 1 < args.Length:
                        model = args[++i];
                        break;
                    case "--test" when i + 1 < args.Length:
                        testType = args[++i].ToLower();
                        break;
                    case "--preload":
                        preloadModel = true;
                        break;
                    case "--low-spec":
                        lowSpecMode = true;
                        break;
                    case "--help":
                        ShowHelp();
                        return;
                }
            }

            using var testScript = new OllamaTestScript(baseUrl, lowSpecMode);

            try
            {
                // Preload model if requested
                if (preloadModel)
                {
                    Console.WriteLine($"🚀 Preloading model '{model}'...");
                    Console.WriteLine("⏱️ This will take ~2-3 minutes for the first load...");
                    
                    var preloadSuccess = await testScript.WarmupModelAsync(model);
                    if (preloadSuccess)
                    {
                        Console.WriteLine("✅ Model preloaded! Subsequent requests will be much faster.");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Model preload failed, but continuing with tests...");
                        Console.WriteLine();
                    }
                }

                switch (testType)
                {
                    case "basic":
                        await testScript.RunBasicTestAsync(model);
                        break;
                    case "streaming":
                        if (string.IsNullOrEmpty(model))
                        {
                            Console.WriteLine("❌ Model required for streaming test. Use --model <model-name>");
                            return;
                        }
                        await testScript.RunStreamingTestAsync(model);
                        break;
                    case "minecraft":
                        if (string.IsNullOrEmpty(model))
                        {
                            Console.WriteLine("❌ Model required for Minecraft test. Use --model <model-name>");
                            return;
                        }
                        await testScript.RunMinecraftTestAsync(model);
                        break;
                    case "temperature":
                        if (string.IsNullOrEmpty(model))
                        {
                            Console.WriteLine("❌ Model required for temperature test. Use --model <model-name>");
                            return;
                        }
                        await testScript.RunTemperatureTestAsync(model);
                        break;
                    case "all":
                        await testScript.RunBasicTestAsync(model);
                        if (!string.IsNullOrEmpty(model))
                        {
                            Console.WriteLine("\n" + "=".PadRight(60, '=') + "\n");
                            await testScript.RunStreamingTestAsync(model);
                            Console.WriteLine("\n" + "=".PadRight(60, '=') + "\n");
                            await testScript.RunMinecraftTestAsync(model);
                        }
                        break;
                    default:
                        Console.WriteLine($"❌ Unknown test type: {testType}");
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error running test: {ex.Message}");
            }

            Console.WriteLine("\n✅ Test completed!");
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage: OllamaTestScript [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --url <url>        Ollama base URL (default: http://localhost:11434)");
            Console.WriteLine("  --model <name>     Model name to test with");
            Console.WriteLine("  --test <type>      Test type to run:");
            Console.WriteLine("                     basic       - Basic functionality test");
            Console.WriteLine("                     streaming   - Streaming response test");
            Console.WriteLine("                     minecraft   - Minecraft-specific prompts test");
            Console.WriteLine("                     temperature - Temperature variation test");
            Console.WriteLine("                     all         - Run all tests");
            Console.WriteLine("  --preload          Preload model before running tests (eliminates first-request delay)");
            Console.WriteLine("  --low-spec         Optimize for low-spec systems (CPU-only mode, smaller batches)");
            Console.WriteLine("  --help             Show this help message");
            Console.WriteLine();
            Console.WriteLine("Environment Variables:");
            Console.WriteLine("  DEBUG_METRICS      Show raw metrics for debugging (true/false)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  OllamaTestScript --test basic");
            Console.WriteLine("  OllamaTestScript --model codellama:7b --test streaming");
            Console.WriteLine("  OllamaTestScript --model phi3:mini --test minecraft");
            Console.WriteLine("  OllamaTestScript --url http://192.168.1.100:11434 --test all");
            Console.WriteLine("  OllamaTestScript --preload --test basic");
            Console.WriteLine("  OllamaTestScript --low-spec --model codellama:7b-instruct-q4_0 --test basic");
        }
    }
}
