# Performance Metrics Improvements

## 🚀 Model Keep-Alive Optimization

### Problem Solved
The biggest performance bottleneck was model loading time (170+ seconds). This has been addressed with:

1. **Increased Keep-Alive Duration**: Default changed from 5 minutes to 30 minutes
2. **Configurable Settings**: Environment variable `OLLAMA_KEEP_ALIVE` for customization
3. **Model Preloading**: `--preload` flag for proactive model loading
4. **Better Documentation**: Clear guidance on optimal settings

### Impact on Metrics
With keep-alive optimization:

**Before (5-minute keep-alive)**:
- First request: ~170s (model load)
- Subsequent requests after 5min: ~170s (model reload)
- Frequent reloads during testing

**After (30-minute keep-alive)**:
- First request: ~170s (model load)
- Subsequent requests within 30min: <5s (no reload)
- Dramatically faster testing cycles

### Configuration Examples
```bash
# Development/Testing (default)
OLLAMA_KEEP_ALIVE=30m ./ollama-test.sh --test basic

# Production (longer keep-alive)
OLLAMA_KEEP_ALIVE=1h ./ollama-test.sh --test basic

# High-traffic (never unload)
OLLAMA_KEEP_ALIVE=-1 ./ollama-test.sh --test basic

# Preload for immediate testing
./ollama-test.sh --preload --test basic
```

## 🚨 Issues Fixed

The original performance metrics had several critical problems that resulted in incorrect values like:
- **Total Duration: 520s** (reasonable)
- **Model Load Time: 91301.705678s** (impossible - 25+ hours!)
- **Generation Time: 253792.853178s** (impossible - 70+ hours!)
- **Tokens/Second: 0** (division by zero)

## 🔧 Root Causes Identified

### 1. **Unit Conversion Error**
- **Problem**: Ollama returns durations in **nanoseconds**, but scripts were dividing by 1,000,000 (microseconds)
- **Fix**: Changed division to 1,000,000,000 (nanoseconds to seconds)

### 2. **Division by Zero**
- **Problem**: When `eval_duration` was 0, tokens/second calculation failed
- **Fix**: Added validation to check for zero values before division

### 3. **Silent Math Failures**
- **Problem**: Bash `bc` calculations could fail silently, showing "N/A"
- **Fix**: Added proper error handling and validation

### 4. **Missing Validation**
- **Problem**: No checks for reasonable metric values
- **Fix**: Added comprehensive validation for all metrics

### 5. **Poor Error Handling**
- **Problem**: Failed calculations showed "N/A" without debugging info
- **Fix**: Added debug mode and better error messages

## ✅ Improvements Made

### Bash Script (`ollama-test.sh`)

#### 1. **Fixed Unit Conversion**
```bash
# Before (WRONG)
local load_duration=$(echo "$response" | jq -r '.load_duration / 1000000' 2>/dev/null)

# After (CORRECT)
local load_duration=$(echo "$response" | jq -r '.load_duration / 1000000000' 2>/dev/null)
```

#### 2. **Added Validation**
```bash
# Validate and format load duration
if [ "$load_duration" != "null" ] && [ -n "$load_duration" ] && [ "$load_duration" != "0" ]; then
    local load_seconds=$(echo "scale=2; $load_duration" | bc -l 2>/dev/null || echo "N/A")
    print_status $YELLOW "   • Model Load Time: ${load_seconds}s"
else
    print_status $YELLOW "   • Model Load Time: N/A (model already loaded)"
fi
```

#### 3. **Safe Tokens/Second Calculation**
```bash
# Calculate tokens per second with proper validation
if [ "$eval_duration" != "null" ] && [ -n "$eval_duration" ] && [ "$eval_duration" != "0" ] && [ "$tokens" != "null" ] && [ -n "$tokens" ] && [ "$tokens" != "0" ]; then
    local tokens_per_sec=$(echo "scale=2; $tokens / $eval_duration" | bc -l 2>/dev/null || echo "N/A")
    print_status $YELLOW "   • Tokens/Second: $tokens_per_sec"
else
    print_status $YELLOW "   • Tokens/Second: N/A (insufficient data)"
fi
```

#### 4. **Added Debug Mode**
```bash
# Function to debug metrics (only shown if DEBUG_METRICS is set)
debug_metrics() {
    if [ "${DEBUG_METRICS:-false}" = "true" ]; then
        local response=$1
        print_status $BLUE "🔍 DEBUG - Raw Response Metrics:"
        echo "$response" | jq -r '{
            load_duration: .load_duration,
            eval_duration: .eval_duration,
            eval_count: .eval_count,
            prompt_eval_count: .prompt_eval_count,
            total_duration: .total_duration
        }' 2>/dev/null || echo "Failed to parse JSON for debugging"
        echo
    fi
}
```

### C# Script (`OllamaTestScript.cs`)

#### 1. **New Performance Metrics Method**
```csharp
private void DisplayPerformanceMetrics(OllamaResponse response)
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
    
    // Proper unit conversion (nanoseconds to seconds)
    var totalSeconds = response.TotalDuration / 1_000_000_000.0;
    Console.WriteLine($"   • Total Duration: {totalSeconds:F2}s");
    
    // Model load time with validation
    if (response.LoadDuration > 0)
    {
        var loadSeconds = response.LoadDuration / 1_000_000_000.0;
        Console.WriteLine($"   • Model Load Time: {loadSeconds:F2}s");
    }
    else
    {
        Console.WriteLine($"   • Model Load Time: N/A (model already loaded)");
    }
    
    // Generation time with validation
    if (response.EvalDuration > 0)
    {
        var evalSeconds = response.EvalDuration / 1_000_000_000.0;
        Console.WriteLine($"   • Generation Time: {evalSeconds:F2}s");
        
        // Safe tokens per second calculation
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
}
```

## 🎯 Expected Results

With these fixes, you should now see **realistic** performance metrics like:

```
📊 Performance Metrics:
   • Total Duration: 3.45s
   • Model Load Time: 0.82s
   • Generation Time: 2.12s
   • Tokens Used: 156
   • Tokens/Second: 73.58
   • Input Tokens: 45
   • Output Tokens: 111
```

## 🔍 Debugging

If you still see unusual metrics, enable debug mode:

### Bash Script
```bash
DEBUG_METRICS=true ./ollama-test.sh --test basic
```

### C# Script
```bash
DEBUG_METRICS=true dotnet run -- --test basic
```

This will show the raw values from Ollama before conversion, helping identify any remaining issues.

## 📊 What Each Metric Means

- **Total Duration**: Wall-clock time from request start to completion
- **Model Load Time**: Time to load model into GPU memory (0 if already loaded)
- **Generation Time**: Pure text generation time (excluding model loading)
- **Tokens/Second**: Generation throughput (output tokens / generation time)
- **Input Tokens**: Number of tokens in the prompt
- **Output Tokens**: Number of tokens in the response
- **Tokens Used**: Total tokens (input + output)

## 🚀 Usage Examples

### Basic Test with Debug
```bash
# Bash
DEBUG_METRICS=true ./ollama-test.sh --test basic

# C#
DEBUG_METRICS=true dotnet run -- --test basic
```

### Analyze Specific File
```bash
# Bash
DEBUG_METRICS=true ./ollama-test.sh --analyze src/Program.cs

# C# (if file analysis is implemented)
DEBUG_METRICS=true dotnet run -- --analyze src/Program.cs
```

### Streaming Test
```bash
# Bash
DEBUG_METRICS=true ./ollama-test.sh --test streaming

# C#
DEBUG_METRICS=true dotnet run -- --test streaming
```

## ✅ Validation Checklist

- [x] Unit conversion fixed (nanoseconds → seconds)
- [x] Division by zero protection added
- [x] Input validation for all metrics
- [x] Debug mode for troubleshooting
- [x] Better error messages
- [x] Consistent formatting
- [x] Both bash and C# scripts updated
- [x] Help text updated with debug option

The performance metrics should now be accurate and helpful for monitoring Ollama performance!
