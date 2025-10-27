#!/bin/bash

# Ollama Test Script - Simple Bash version
# This script provides basic Ollama API testing functionality

# Configuration
OLLAMA_URL="${OLLAMA_URL:-${AI__OllamaBaseUrl:-http://localhost:11434}}"
MODEL="${MODEL:-codellama:7b}"
TEST_TYPE="${TEST_TYPE:-basic}"
PRELOAD_MODEL="${PRELOAD_MODEL:-false}"
PROFILE_MODE="${PROFILE_MODE:-false}"
LOW_SPEC_MODE="${LOW_SPEC_MODE:-false}"

# Performance optimization environment variables
# These are the most critical settings users have found effective
export OLLAMA_MAX_LOADED_MODELS="${OLLAMA_MAX_LOADED_MODELS:-1}"
export OLLAMA_NUM_PARALLEL="${OLLAMA_NUM_PARALLEL:-1}"
export OLLAMA_FLASH_ATTENTION="${OLLAMA_FLASH_ATTENTION:-1}"
export OLLAMA_GPU_MEMORY_FRACTION="${OLLAMA_GPU_MEMORY_FRACTION:-0.9}"
export OLLAMA_KEEP_ALIVE="${OLLAMA_KEEP_ALIVE:-30m}"
export OLLAMA_HOST="${OLLAMA_HOST:-0.0.0.0:11434}"

# Advanced GPU optimization settings
export OLLAMA_NUM_GPU="${OLLAMA_NUM_GPU:-1}"
export OLLAMA_NUM_THREAD="${OLLAMA_NUM_THREAD:-8}"
export OLLAMA_BATCH_SIZE="${OLLAMA_BATCH_SIZE:-512}"
export OLLAMA_CONTEXT_SIZE="${OLLAMA_CONTEXT_SIZE:-4096}"

# Low-spec system optimizations
if [ "$LOW_SPEC_MODE" = "true" ]; then
    print_status $YELLOW "🔧 Applying low-spec system optimizations..."
    export OLLAMA_NUM_GPU=0  # Force CPU-only mode
    export OLLAMA_NUM_THREAD=4  # Conservative threading
    export OLLAMA_GPU_MEMORY_FRACTION=0.0  # No GPU memory
    export OLLAMA_BATCH_SIZE=128  # Smaller batches
    export OLLAMA_CONTEXT_SIZE=2048  # Smaller context window
    export OLLAMA_MAX_LOADED_MODELS=1  # Single model only
    print_status $GREEN "✅ Low-spec optimizations applied (CPU-only mode)"
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

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

# Function to check if Ollama is available
check_ollama() {
    print_status $BLUE "🔍 Checking Ollama availability..."

    if curl -s "${OLLAMA_URL}/api/tags" > /dev/null 2>&1; then
        print_status $GREEN "✅ Ollama service is available at ${OLLAMA_URL}"
        return 0
    else
        print_status $RED "❌ Ollama service is not available at ${OLLAMA_URL}"
        print_status $YELLOW "Please ensure Ollama is running and accessible"
        return 1
    fi
}

# Function to warm up model (critical for performance)
warmup_model() {
    local model=$1

    print_status $BLUE "🔥 Warming up model '$model'..."

    # Send a minimal warmup request
    local warmup_data=$(jq -n \
        --arg model "$model" \
        --arg prompt "Hi" \
        --argjson stream false \
        '{
            model: $model,
            prompt: $prompt,
            stream: $stream,
            options: {
                temperature: 0.1,
                num_predict: 1,
                num_ctx: 64
            }
        }')

    local warmup_response=$(curl -s -X POST "${OLLAMA_URL}/api/generate" \
        -H "Content-Type: application/json" \
        -d "$warmup_data")

    if [ $? -eq 0 ]; then
        print_status $GREEN "✅ Model '$model' warmed up successfully"
        return 0
    else
        print_status $YELLOW "⚠️ Model warmup failed, but continuing..."
        return 1
    fi
}

# Function to check system resources (critical for performance)
check_system_resources() {
    print_status $BLUE "🔍 Checking system resources..."

    # Check available memory
    local available_memory=$(free -m | awk 'NR==2{printf "%.1f", $7/1024}')
    local total_memory=$(free -m | awk 'NR==2{printf "%.1f", $2/1024}')

    print_status $YELLOW "📊 Memory: ${available_memory}GB available / ${total_memory}GB total"

    # Check CPU load
    local cpu_load=$(uptime | awk -F'load average:' '{print $2}' | awk '{print $1}' | sed 's/,//')
    print_status $YELLOW "📊 CPU Load: $cpu_load"

    # Check if Ollama process is running
    local ollama_processes=$(pgrep -c ollama 2>/dev/null || echo "0")
    print_status $YELLOW "📊 Ollama Processes: $ollama_processes"

    # Warn if resources are low
    if (( $(echo "$available_memory < 2.0" | bc -l) )); then
        print_status $RED "⚠️ Low memory warning: Only ${available_memory}GB available"
        print_status $YELLOW "Consider closing other applications or using a smaller model"
    fi

    if (( $(echo "$cpu_load > 4.0" | bc -l) )); then
        print_status $RED "⚠️ High CPU load warning: $cpu_load"
        print_status $YELLOW "Consider waiting for system load to decrease"
    fi
}

# Function to run performance profiling
run_performance_profile() {
    print_status $BLUE "🔬 Running Performance Profile Analysis"
    echo "═══════════════════════════════════════════════"

    # System resources
    check_system_resources

    # GPU profiling if available
    if command -v nvidia-smi &> /dev/null; then
        print_status $BLUE "🎮 GPU Performance Analysis:"
        print_status $YELLOW "   • GPU Utilization:"
        nvidia-smi --query-gpu=utilization.gpu --format=csv,noheader,nounits | while read util; do
            print_status $YELLOW "     - GPU $((++i)): ${util}%"
        done

        print_status $YELLOW "   • Memory Usage:"
        nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits | while read used total; do
            local percent=$((used * 100 / total))
            print_status $YELLOW "     - GPU $((++i)): ${used}MB / ${total}MB (${percent}%)"
        done

        print_status $YELLOW "   • Temperature:"
        nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader,nounits | while read temp; do
            print_status $YELLOW "     - GPU $((++i)): ${temp}°C"
        done
        echo
    fi

    # Ollama-specific profiling
    print_status $BLUE "🤖 Ollama Performance Settings:"
    print_status $YELLOW "   • OLLAMA_GPU_MEMORY_FRACTION: ${OLLAMA_GPU_MEMORY_FRACTION:-0.9}"
    print_status $YELLOW "   • OLLAMA_NUM_GPU: ${OLLAMA_NUM_GPU:-1}"
    print_status $YELLOW "   • OLLAMA_NUM_THREAD: ${OLLAMA_NUM_THREAD:-8}"
    print_status $YELLOW "   • OLLAMA_BATCH_SIZE: ${OLLAMA_BATCH_SIZE:-512}"
    print_status $YELLOW "   • OLLAMA_CONTEXT_SIZE: ${OLLAMA_CONTEXT_SIZE:-4096}"
    print_status $YELLOW "   • OLLAMA_FLASH_ATTENTION: ${OLLAMA_FLASH_ATTENTION:-1}"
    echo

    # Performance recommendations
    print_status $BLUE "💡 Performance Recommendations:"
    local available_memory=$(free -m | awk 'NR==2{printf "%.1f", $7/1024}')
    local cpu_cores=$(nproc)

    if (( $(echo "$available_memory > 16.0" | bc -l) )); then
        print_status $GREEN "   ✅ High memory available - consider increasing OLLAMA_GPU_MEMORY_FRACTION to 0.95"
    elif (( $(echo "$available_memory > 8.0" | bc -l) )); then
        print_status $YELLOW "   ⚠️ Moderate memory - current settings should work well"
    else
        print_status $RED "   ❌ Low memory - consider reducing OLLAMA_GPU_MEMORY_FRACTION to 0.7"
    fi

    if [ "$cpu_cores" -ge 8 ]; then
        print_status $GREEN "   ✅ High CPU core count - consider increasing OLLAMA_NUM_THREAD to 12"
    elif [ "$cpu_cores" -ge 4 ]; then
        print_status $YELLOW "   ⚠️ Moderate CPU cores - current settings should work well"
    else
        print_status $RED "   ❌ Low CPU cores - consider reducing OLLAMA_NUM_THREAD to 4"
    fi

    echo
}

# Function to recommend quantized models for low-spec systems
recommend_quantized_models() {
    print_status $BLUE "💡 Quantized Model Recommendations for Low-Spec Systems:"
    echo "════════════════════════════════════════════════════════════════"

    print_status $YELLOW "🔹 CodeLlama 7B Quantized Versions:"
    print_status $GREEN "   • codellama:7b-instruct-q4_0 (~4GB RAM) - Best for low memory"
    print_status $GREEN "   • codellama:7b-instruct-q8_0 (~7GB RAM) - Better quality"
    print_status $GREEN "   • codellama:7b-instruct-q2_k (~3GB RAM) - Ultra-low memory"

    print_status $YELLOW "🔹 Alternative Lightweight Models:"
    print_status $GREEN "   • phi3:mini (~2.3GB) - Microsoft's efficient model"
    print_status $GREEN "   • qwen2.5-coder:0.5b (~0.5GB) - Ultra-lightweight"
    print_status $GREEN "   • deepseek-coder:1.3b (~1.3GB) - Fast inference"

    print_status $YELLOW "🔹 Installation Commands:"
    print_status $BLUE "   ollama pull codellama:7b-instruct-q4_0"
    print_status $BLUE "   ollama pull phi3:mini"
    print_status $BLUE "   ollama pull qwen2.5-coder:0.5b"

    print_status $YELLOW "🔹 Usage with Low-Spec Mode:"
    print_status $BLUE "   ./ollama-test.sh --low-spec --model codellama:7b-instruct-q4_0"
    print_status $BLUE "   ./ollama-test.sh --low-spec --model phi3:mini"

    echo
}

# Function to get available models
get_models() {
    print_status $BLUE "📋 Getting available models..."

    local models=$(curl -s "${OLLAMA_URL}/api/tags" | jq -r '.models[].name' 2>/dev/null)

    if [ -z "$models" ]; then
        print_status $RED "❌ No models found or jq not installed"
        print_status $YELLOW "Please install jq: sudo apt-get install jq (Ubuntu/Debian) or brew install jq (macOS)"
        return 1
    fi

    print_status $GREEN "✅ Available models:"
    echo "$models" | while read -r model; do
        echo "   • $model"
    done

    # Set default model if not specified
    if [ -z "$MODEL" ] || [ "$MODEL" = "llamacode:7bn" ]; then
        MODEL=$(echo "$models" | head -n1)
        print_status $YELLOW "Using first available model: $MODEL"
    fi
}

# Function to ensure a valid model is available (fail-safe)
ensure_model_available() {
    local primary_model="$MODEL"
    local fallback_models=("phi3:mini" "qwen2.5-coder:0.5b" "codellama:7b-instruct-q4_0")

    print_status $BLUE "🔍 Checking availability of model '$primary_model'..."
    local model_available=$(curl -s "${OLLAMA_URL}/api/tags" | jq -r ".models[].name" | grep -Fx "$primary_model")

    if [ -n "$model_available" ]; then
        print_status $GREEN "✅ Model '$primary_model' is available."
        return 0
    fi

    print_status $RED "❌ Model '$primary_model' not found."
    print_status $YELLOW "🔄 Attempting to use a fallback model..."

    for backup_model in "${fallback_models[@]}"; do
        local available=$(curl -s "${OLLAMA_URL}/api/tags" | jq -r ".models[].name" | grep -Fx "$backup_model")
        if [ -n "$available" ]; then
            MODEL="$backup_model"
            print_status $GREEN "✅ Fallback successful: using '$MODEL' instead."
            return 0
        fi
    done

    print_status $RED "❌ No available fallback models found."
    print_status $YELLOW "💡 You can install one with: ollama pull ${fallback_models[0]}"
    exit 1
}


# Function to estimate processing time
estimate_processing_time() {
    local prompt=$1
    local model=$2

    # Count words and lines in prompt
    local word_count=$(echo "$prompt" | wc -w)
    local line_count=$(echo "$prompt" | wc -l)
    local char_count=$(echo "$prompt" | wc -c)

    # Base time estimates (in seconds)
    local base_time=2
    local word_factor=0.01
    local line_factor=0.1
    local char_factor=0.001

    # Model-specific adjustments
    local model_factor=1.0
    case $model in
        *mini*|*small*)
            model_factor=0.7
            ;;
        *large*|*big*)
            model_factor=1.5
            ;;
        *7b*)
            model_factor=1.0
            ;;
        *13b*)
            model_factor=1.3
            ;;
        *70b*)
            model_factor=2.0
            ;;
    esac

    # Calculate estimated time
    local estimated_time=$(echo "scale=1; ($base_time + $word_count * $word_factor + $line_count * $line_factor + $char_count * $char_factor) * $model_factor" | bc -l 2>/dev/null || echo "5.0")

    # Round to nearest 0.5
    estimated_time=$(echo "scale=1; ($estimated_time + 0.25) / 0.5 * 0.5" | bc -l 2>/dev/null || echo "5.0")

    echo "$estimated_time"
}

# Function to show processing time estimate
show_time_estimate() {
    local prompt=$1
    local model=$2

    local estimated_time=$(estimate_processing_time "$prompt" "$model")
    print_status $YELLOW "⏱️ Estimated processing time: ${estimated_time}s"

    # Show complexity indicators
    local word_count=$(echo "$prompt" | wc -w)
    local line_count=$(echo "$prompt" | wc -l)

    if [ $word_count -gt 100 ]; then
        print_status $YELLOW "📊 Complex prompt detected (${word_count} words, ${line_count} lines)"
    elif [ $word_count -gt 50 ]; then
        print_status $YELLOW "📊 Medium complexity prompt (${word_count} words, ${line_count} lines)"
    else
        print_status $YELLOW "📊 Simple prompt (${word_count} words, ${line_count} lines)"
    fi
}
show_progress() {
    local message=$1
    local pid=$2

    local spin='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0

    while kill -0 $pid 2>/dev/null; do
        printf "\r${spin:$i:1} $message"
        i=$(( (i+1) % 10 ))
        sleep 0.1
    done
    printf "\r✅ $message\n"
}

# Function to show progress with estimated time
show_progress_with_time() {
    local message=$1
    local pid=$2
    local start_time=$(date +%s)

    local spin='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0

    while kill -0 $pid 2>/dev/null; do
        local elapsed=$(($(date +%s) - start_time))
        printf "\r${spin:$i:1} $message (${elapsed}s)"
        i=$(( (i+1) % 10 ))
        sleep 0.1
    done
    local total_time=$(($(date +%s) - start_time))
    printf "\r✅ $message (completed in ${total_time}s)\n"
}

# Function to show thinking status
show_thinking_status() {
    local message=$1
    local duration=${2:-5}

    local thoughts=(
        "Analyzing code structure..."
        "Checking for potential issues..."
        "Reviewing best practices..."
        "Evaluating performance..."
        "Generating recommendations..."
        "Finalizing analysis..."
    )

    local i=0
    local start_time=$(date +%s)

    while [ $(($(date +%s) - start_time)) -lt $duration ]; do
        local thought="${thoughts[$i]}"
        printf "\r🤔 $message - $thought"
        i=$(( (i+1) % ${#thoughts[@]} ))
        sleep 0.8
    done
    printf "\r🤔 $message - Processing complete!\n"
}
send_request() {
    local model=$1
    local prompt=$2
    local temperature=${3:-0.7}
    local max_tokens=${4:-200}

    print_status $BLUE "🚀 Sending request to model '$model'..."
    print_status $YELLOW "📝 Prompt: ${prompt:0:100}$([ ${#prompt} -gt 100 ] && echo "...")"
    print_status $YELLOW "⚙️ Temperature: $temperature, Max Tokens: $max_tokens"

    # Show time estimate
    show_time_estimate "$prompt" "$model"
    echo

    # Use jq to properly escape the JSON with optimized parameters
    # These settings are based on user-proven optimizations
    local request_data=$(jq -n \
        --arg model "$model" \
        --arg prompt "$prompt" \
        --argjson stream false \
        --argjson temperature "$temperature" \
        --argjson num_predict "$max_tokens" \
        '{
            model: $model,
            prompt: $prompt,
            stream: $stream,
            options: {
                temperature: $temperature,
                num_predict: $num_predict,
                num_ctx: 2048,
                num_batch: 8,
                num_thread: 4,
                repeat_penalty: 1.1,
                top_k: 40,
                top_p: 0.9
            }
        }')

    # Start the request in background and show progress
    local start_time=$(date +%s)
    print_status $BLUE "🤔 AI is thinking..."

    # Show thinking status for a few seconds
    show_thinking_status "Processing request" 3 &
    local thinking_pid=$!

    # Make the actual request
    local response=$(curl -s -X POST "${OLLAMA_URL}/api/generate" \
        -H "Content-Type: application/json" \
        -d "$request_data")

    # Stop the thinking animation
    kill $thinking_pid 2>/dev/null
    wait $thinking_pid 2>/dev/null

    local end_time=$(date +%s)
    local duration=$((end_time - start_time))

    if [ $? -eq 0 ]; then
        # Debug metrics if enabled
        debug_metrics "$response"

        local content=$(echo "$response" | jq -r '.response' 2>/dev/null)
        local tokens=$(echo "$response" | jq -r '.eval_count + .prompt_eval_count' 2>/dev/null)
        local load_duration=$(echo "$response" | jq -r '.load_duration / 1000000000' 2>/dev/null)
        local eval_duration=$(echo "$response" | jq -r '.eval_duration / 1000000000' 2>/dev/null)

        if [ "$content" != "null" ] && [ -n "$content" ]; then
            print_status $GREEN "✅ Response received in ${duration}s:"
            echo
            echo "$content"
            echo
            print_status $YELLOW "📊 Performance Metrics:"
            print_status $YELLOW "   • Total Duration: ${duration}s"

            # Validate and format load duration
            if [ "$load_duration" != "null" ] && [ -n "$load_duration" ] && [ "$load_duration" != "0" ]; then
                local load_seconds=$(echo "scale=2; $load_duration" | bc -l 2>/dev/null || echo "N/A")
                print_status $YELLOW "   • Model Load Time: ${load_seconds}s"
            else
                print_status $YELLOW "   • Model Load Time: N/A (model already loaded)"
            fi

            # Validate and format generation duration
            if [ "$eval_duration" != "null" ] && [ -n "$eval_duration" ] && [ "$eval_duration" != "0" ]; then
                local gen_seconds=$(echo "scale=2; $eval_duration" | bc -l 2>/dev/null || echo "N/A")
                print_status $YELLOW "   • Generation Time: ${gen_seconds}s"
            else
                print_status $YELLOW "   • Generation Time: N/A"
            fi

            print_status $YELLOW "   • Tokens Used: $tokens"

            # Calculate tokens per second with proper validation
            if [ "$eval_duration" != "null" ] && [ -n "$eval_duration" ] && [ "$eval_duration" != "0" ] && [ "$tokens" != "null" ] && [ -n "$tokens" ] && [ "$tokens" != "0" ]; then
                local tokens_per_sec=$(echo "scale=2; $tokens / $eval_duration" | bc -l 2>/dev/null || echo "N/A")
                print_status $YELLOW "   • Tokens/Second: $tokens_per_sec"
            else
                print_status $YELLOW "   • Tokens/Second: N/A (insufficient data)"
            fi
        else
            print_status $RED "❌ Failed to parse response"
            echo "Raw response: $response"
        fi
    else
        print_status $RED "❌ Request failed"
    fi
}

# Function to send a streaming request
send_streaming_request() {
    local model=$1
    local prompt=$2
    local temperature=${3:-0.7}
    local max_tokens=${4:-500}

    print_status $BLUE "🚀 Sending streaming request to model '$model'..."
    print_status $YELLOW "📝 Prompt: ${prompt:0:100}$([ ${#prompt} -gt 100 ] && echo "...")"
    print_status $YELLOW "⚙️ Temperature: $temperature, Max Tokens: $max_tokens"
    echo
    print_status $BLUE "📡 Starting streaming response..."
    echo "────────────────────────────────────────────────────────"

    # Use jq to properly escape the JSON
    local request_data=$(jq -n \
        --arg model "$model" \
        --arg prompt "$prompt" \
        --argjson stream true \
        --argjson temperature "$temperature" \
        --argjson num_predict "$max_tokens" \
        '{
            model: $model,
            prompt: $prompt,
            stream: $stream,
            options: {
                temperature: $temperature,
                num_predict: $num_predict
            }
        }')

    local start_time=$(date +%s)
    local token_count=0
    local first_token_time=0

    curl -s -X POST "${OLLAMA_URL}/api/generate" \
        -H "Content-Type: application/json" \
        -d "$request_data" | \
    while IFS= read -r line; do
        if [ -n "$line" ]; then
            local content=$(echo "$line" | jq -r '.response // empty' 2>/dev/null)
            local done=$(echo "$line" | jq -r '.done // false' 2>/dev/null)
            local eval_count=$(echo "$line" | jq -r '.eval_count // 0' 2>/dev/null)
            local prompt_eval_count=$(echo "$line" | jq -r '.prompt_eval_count // 0' 2>/dev/null)

            if [ -n "$content" ]; then
                if [ $first_token_time -eq 0 ]; then
                    first_token_time=$(date +%s)
                    local time_to_first_token=$((first_token_time - start_time))
                    print_status $GREEN "⚡ First token received in ${time_to_first_token}s"
                fi

                echo -n "$content"
                token_count=$((token_count + 1))
            fi

            if [ "$done" = "true" ]; then
                echo
                echo "────────────────────────────────────────────────────────"
                local end_time=$(date +%s)
                local total_duration=$((end_time - start_time))
                local generation_duration=$((end_time - first_token_time))
                local total_tokens=$((eval_count + prompt_eval_count))

                print_status $GREEN "✅ Streaming completed!"
                print_status $YELLOW "📊 Performance Metrics:"
                print_status $YELLOW "   • Total Duration: ${total_duration}s"
                print_status $YELLOW "   • Time to First Token: ${time_to_first_token}s"
                print_status $YELLOW "   • Generation Duration: ${generation_duration}s"
                print_status $YELLOW "   • Total Tokens: $total_tokens"

                # Calculate tokens per second with proper validation
                if [ "$generation_duration" -gt 0 ] && [ "$total_tokens" -gt 0 ]; then
                    local tokens_per_sec=$(echo "scale=2; $total_tokens / $generation_duration" | bc -l 2>/dev/null || echo "N/A")
                    print_status $YELLOW "   • Tokens/Second: $tokens_per_sec"
                else
                    print_status $YELLOW "   • Tokens/Second: N/A (insufficient data)"
                fi
                break
            fi
        fi
    done
}

# Function to run basic test
run_basic_test() {
    print_status $BLUE "🧪 Running Basic Ollama Test"
    echo "═══════════════════════════════"

    if ! check_ollama; then
        return 1
    fi

    echo
    # Check system resources before testing
    check_system_resources
    echo

    if ! get_models; then
        return 1
    fi

    echo
    print_status $BLUE "🧪 Testing with model '$MODEL'..."

    # Warm up the model first (critical for performance)
    warmup_model "$MODEL"
    echo

    local test_prompt="Analyze this C# code and explain what it does:

public class ExampleClass
{
    private string _name;

    public ExampleClass(string name)
    {
        _name = name;
    }

    public string GetName() => _name;

    public void SetName(string name)
    {
        _name = name;
    }
}"
    send_request "$MODEL" "$test_prompt" 0.7 300
}

# Function to run streaming test
run_streaming_test() {
    print_status $BLUE "🧪 Running Streaming Test with '$MODEL'"
    echo "═══════════════════════════════════════════"

    local test_prompt="Review this JavaScript function and suggest improvements:

function calculateTotal(items) {
    var total = 0;
    for (var i = 0; i < items.length; i++) {
        total = total + items[i].price;
    }
    return total;
}"
    send_streaming_request "$MODEL" "$test_prompt" 0.7 400
}

# Function to run code analysis test
run_code_analysis_test() {
    print_status $BLUE "🧪 Running Code Analysis Test with '$MODEL'"
    echo "═══════════════════════════════════════════════════"

    local prompts=(
        "Analyze this Python code for potential issues:

def process_data(data):
    result = []
    for item in data:
        if item > 0:
            result.append(item * 2)
    return result"

        "Review this C# method and suggest optimizations:

public List<string> GetFileNames(string directory)
{
    List<string> files = new List<string>();
    foreach (string file in Directory.GetFiles(directory))
    {
        files.Add(Path.GetFileName(file));
    }
    return files;
}"

        "Explain what this SQL query does and identify any potential problems:

SELECT u.name, COUNT(o.id) as order_count
FROM users u
LEFT JOIN orders o ON u.id = o.user_id
WHERE u.created_at > '2023-01-01'
GROUP BY u.id
ORDER BY order_count DESC;"

        "Analyze this Java class for design patterns and suggest improvements:

public class UserService {
    private DatabaseConnection db;

    public UserService() {
        db = new DatabaseConnection();
    }

    public User getUser(int id) {
        return db.query(\"SELECT * FROM users WHERE id = \" + id);
    }
}"

        "Review this TypeScript interface and suggest enhancements:

interface User {
    id: number;
    name: string;
    email: string;
    age: number;
}

function createUser(userData: any): User {
    return {
        id: Math.random(),
        name: userData.name,
        email: userData.email,
        age: userData.age
    };
}"
    )

    for prompt in "${prompts[@]}"; do
        echo
        print_status $YELLOW "📝 Prompt: $prompt"
        echo "────────────────────────────────────────────────────────"

        send_request "$MODEL" "$prompt" 0.7 300
        echo
    done
}

# Function to analyze a specific code file
analyze_code_file() {
    local file_path=$1
    local model=${2:-$MODEL}

    if [ ! -f "$file_path" ]; then
        print_status $RED "❌ File not found: $file_path"
        return 1
    fi

    print_status $BLUE "🔍 Analyzing code file: $file_path"
    echo "════════════════════════════════════════════════════════════════"

    # Read the file content
    local file_content=$(cat "$file_path")
    local file_extension="${file_path##*.}"

    # Create analysis prompt based on file type
    local analysis_prompt=""
    case $file_extension in
        cs|csharp)
            analysis_prompt="Analyze this C# code file and provide:
1. What the code does
2. Potential issues or bugs
3. Performance improvements
4. Code quality suggestions
5. Best practices recommendations

Code:
\`\`\`csharp
$file_content
\`\`\`"
            ;;
        js|javascript)
            analysis_prompt="Analyze this JavaScript code file and provide:
1. What the code does
2. Potential issues or bugs
3. Performance improvements
4. Code quality suggestions
5. Best practices recommendations

Code:
\`\`\`javascript
$file_content
\`\`\`"
            ;;
        py|python)
            analysis_prompt="Analyze this Python code file and provide:
1. What the code does
2. Potential issues or bugs
3. Performance improvements
4. Code quality suggestions
5. Best practices recommendations

Code:
\`\`\`python
$file_content
\`\`\`"
            ;;
        java)
            analysis_prompt="Analyze this Java code file and provide:
1. What the code does
2. Potential issues or bugs
3. Performance improvements
4. Code quality suggestions
5. Best practices recommendations

Code:
\`\`\`java
$file_content
\`\`\`"
            ;;
        *)
            analysis_prompt="Analyze this $file_extension code file and provide:
1. What the code does
2. Potential issues or bugs
3. Performance improvements
4. Code quality suggestions
5. Best practices recommendations

Code:
\`\`\`
$file_content
\`\`\`"
            ;;
    esac

    print_status $YELLOW "📝 Analyzing $file_extension file with model '$model'..."
    echo

    # Show analysis progress
    print_status $BLUE "🔍 Starting code analysis..."
    show_thinking_status "Analyzing $file_extension code" 2 &
    local thinking_pid=$!

    send_request "$model" "$analysis_prompt" 0.7 1000

    # Stop the thinking animation
    kill $thinking_pid 2>/dev/null
    wait $thinking_pid 2>/dev/null
}

# Function to run temperature test
run_temperature_test() {
    print_status $BLUE "🧪 Running Temperature Test with '$MODEL'"
    echo "═══════════════════════════════════════════════"

    local prompt="Write a creative short story about a robot."
    local temperatures=(0.1 0.5 0.7 1.0 1.2)

    for temp in "${temperatures[@]}"; do
        echo
        print_status $YELLOW "🌡️ Temperature: $temp"
        echo "────────────────────────────────────────"

        send_request "$MODEL" "$prompt" "$temp" 200
        echo
    done
}

# Function to show help
show_help() {
    echo "🤖 Ollama Test Script (Bash version)"
    echo "═══════════════════════════════════════"
    echo
    echo "Usage: $0 [options] [file]"
    echo
    echo "Options:"
    echo "  --url <url>        Ollama base URL"
    echo "  --model <name>     Model name to test with"
    echo "  --test <type>      Test type to run"
    echo "  --analyze <file>   Analyze a specific code file"
    echo "  --preload          Preload model before running tests (eliminates first-request delay)"
    echo "  --profile          Run performance profiling analysis"
    echo "  --low-spec         Optimize for low-spec systems (CPU-only mode, smaller batches)"
    echo "  --help             Show this help message"
    echo
    echo "Environment Variables:"
    echo "  OLLAMA_URL         Ollama base URL (default: http://localhost:11434)"
    echo "  AI__OllamaBaseUrl  Alternative Ollama base URL (MCMAA style)"
    echo "  MODEL              Model name to test with (default: llamacode:7bn)"
    echo "  TEST_TYPE          Test type to run (default: basic)"
    echo "  DEBUG_METRICS      Show raw metrics for debugging (true/false)"
    echo
    echo "Performance Optimization Variables:"
    echo "  OLLAMA_MAX_LOADED_MODELS  Max models in memory (default: 1)"
    echo "  OLLAMA_NUM_PARALLEL       Parallel requests (default: 1)"
    echo "  OLLAMA_FLASH_ATTENTION    Enable flash attention (default: 1)"
    echo "  OLLAMA_GPU_MEMORY_FRACTION GPU memory usage (default: 0.9)"
    echo "  OLLAMA_KEEP_ALIVE         Keep model loaded duration (default: 30m)"
    echo "  OLLAMA_NUM_GPU            Number of GPU layers (default: 1)"
    echo "  OLLAMA_NUM_THREAD         CPU threads for processing (default: 8)"
    echo "  OLLAMA_BATCH_SIZE         Batch size for processing (default: 512)"
    echo "  OLLAMA_CONTEXT_SIZE       Context window size (default: 4096)"
    echo
    echo "Test Types:"
    echo "  basic          Basic functionality test"
    echo "  streaming      Streaming response test"
    echo "  code           Code analysis and review test"
    echo "  temperature    Temperature variation test"
    echo "  all            Run all tests"
    echo
    echo "Examples:"
    echo "  $0                                    # Run basic test"
    echo "  $0 --analyze src/Program.cs          # Analyze a C# file"
    echo "  $0 --analyze app.js --model phi3:mini # Analyze JS file with specific model"
    echo "  MODEL=phi3:mini $0                   # Use specific model"
    echo "  TEST_TYPE=streaming $0                # Run streaming test"
    echo "  TEST_TYPE=code MODEL=llamacode:7bn $0 # Run code analysis test"
    echo "  OLLAMA_URL=http://192.168.1.100:11434 $0 # Use remote Ollama"
    echo "  AI__OllamaBaseUrl=http://127.0.0.1:11435 $0 # Use MCMAA-style URL"
    echo
    echo "Requirements:"
    echo "  • curl (for HTTP requests)"
    echo "  • jq (for JSON parsing)"
    echo "  • Ollama service running"
}

# Main execution
main() {
    echo "🤖 Ollama Test Script (Bash version)"
    echo "═══════════════════════════════════════"
    echo

    # Parse command line arguments
    local analyze_file=""

    while [[ $# -gt 0 ]]; do
        case $1 in
            --url)
                OLLAMA_URL="$2"
                shift 2
                ;;
            --model)
                MODEL="$2"
                shift 2
                ;;
            --test)
                TEST_TYPE="$2"
                shift 2
                ;;
            --analyze)
                analyze_file="$2"
                shift 2
                ;;
            --preload)
                PRELOAD_MODEL="true"
                shift
                ;;
            --profile)
                PROFILE_MODE="true"
                shift
                ;;
            --low-spec)
                LOW_SPEC_MODE="true"
                shift
                ;;
            --help|-h)
                show_help
                exit 0
                ;;
            *)
                # If it's not a known option, treat it as a file to analyze
                if [ -z "$analyze_file" ] && [ -f "$1" ]; then
                    analyze_file="$1"
                fi
                shift
                ;;
        esac
    done

    # If a file is specified for analysis, analyze it and exit
    if [ -n "$analyze_file" ]; then
        analyze_code_file "$analyze_file" "$MODEL"
        exit $?
    fi

    # Preload model if requested
    if [ "$PRELOAD_MODEL" = "true" ]; then
        print_status $BLUE "🚀 Preloading model '$MODEL'..."
        if ! check_ollama; then
            print_status $RED "❌ Cannot preload model - Ollama service not available"
            exit 1
        fi

        print_status $YELLOW "⏱️ This will take ~2-3 minutes for the first load..."
        warmup_model "$MODEL"
        print_status $GREEN "✅ Model preloaded! Subsequent requests will be much faster."
        echo
    fi

    # Run performance profile if requested
    if [ "$PROFILE_MODE" = "true" ]; then
        run_performance_profile
        exit 0
    fi

    # Show quantized model recommendations for low-spec mode
    if [ "$LOW_SPEC_MODE" = "true" ]; then
        recommend_quantized_models
    fi


    # Ensure we have a working model
    ensure_model_available

    # Run the specified test
    case $TEST_TYPE in
        basic)
            run_basic_test
            ;;
        streaming)
            run_streaming_test
            ;;
        minecraft)
            run_code_analysis_test
            ;;
        code)
            run_code_analysis_test
            ;;
        temperature)
            run_temperature_test
            ;;
        all)
            run_basic_test
            echo
            echo "════════════════════════════════════════════════════════════════"
            echo
            run_streaming_test
            echo
            echo "════════════════════════════════════════════════════════════════"
            echo
            run_code_analysis_test
            ;;
        *)
            print_status $RED "❌ Unknown test type: $TEST_TYPE"
            show_help
            exit 1
            ;;
    esac

    echo
    print_status $GREEN "✅ Test completed!"
}

# Check dependencies
check_dependencies() {
    local missing_deps=()

    if ! command -v curl &> /dev/null; then
        missing_deps+=("curl")
    fi

    if ! command -v jq &> /dev/null; then
        missing_deps+=("jq")
    fi

    if [ ${#missing_deps[@]} -ne 0 ]; then
        print_status $RED "❌ Missing dependencies: ${missing_deps[*]}"
        print_status $YELLOW "Please install the missing dependencies:"
        for dep in "${missing_deps[@]}"; do
            case $dep in
                curl)
                    echo "   • Ubuntu/Debian: sudo apt-get install curl"
                    echo "   • macOS: brew install curl"
                    echo "   • CentOS/RHEL: sudo yum install curl"
                    ;;
                jq)
                    echo "   • Ubuntu/Debian: sudo apt-get install jq"
                    echo "   • macOS: brew install jq"
                    echo "   • CentOS/RHEL: sudo yum install jq"
                    ;;
            esac
        done
        exit 1
    fi
}

# Run main function
check_dependencies
main "$@"
