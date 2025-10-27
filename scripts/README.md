# Ollama Test Scripts

This directory contains standalone test scripts for testing Ollama API functionality outside of the MCMAA configuration. These scripts are useful for:

- Testing Ollama service availability
- Verifying model functionality
- Testing different prompts and parameters
- Debugging Ollama integration issues
- Performance testing

## Available Scripts

### 1. C# Test Script (`OllamaTestScript.cs`)

A comprehensive C# console application with full Ollama API support.

**Features:**
- ✅ Full Ollama API integration
- ✅ Streaming and non-streaming requests
- ✅ Model discovery and management
- ✅ Multiple test scenarios
- ✅ Comprehensive error handling
- ✅ JSON response parsing
- ✅ Performance metrics

**Usage:**
```bash
# Build and run
cd scripts
dotnet build OllamaTestScript.csproj
dotnet run -- --test basic

# Available options
dotnet run -- --help
```

**Examples:**
```bash
# Basic functionality test
dotnet run -- --test basic

# Streaming test with specific model
dotnet run -- --model llamacode:7bn --test streaming

# Minecraft-specific prompts test
dotnet run -- --model phi3:mini --test minecraft

# Temperature variation test
dotnet run -- --model llamacode:7bn --test temperature

# Run all tests
dotnet run -- --test all

# Use remote Ollama instance
dotnet run -- --url http://192.168.1.100:11434 --test basic
```

### 2. Bash Test Script (`ollama-test.sh`)

A lightweight bash script for quick Ollama testing.

**Features:**
- ✅ Basic Ollama API testing
- ✅ Streaming support
- ✅ Multiple test scenarios
- ✅ Colored output
- ✅ Environment variable configuration
- ✅ No dependencies beyond curl and jq

**Usage:**
```bash
# Make executable and run
chmod +x ollama-test.sh
./ollama-test.sh

# Available options
./ollama-test.sh --help
```

**Examples:**
```bash
# Basic test (uses llamacode:7bn by default)
./ollama-test.sh

# Use specific model
MODEL=codellama:7b ./ollama-test.sh

# Run streaming test
TEST_TYPE=streaming ./ollama-test.sh

# Run Minecraft-specific test
TEST_TYPE=minecraft MODEL=llamacode:7bn ./ollama-test.sh

# Run temperature test
TEST_TYPE=temperature ./ollama-test.sh

# Run all tests
TEST_TYPE=all ./ollama-test.sh

# Use remote Ollama
OLLAMA_URL=http://192.168.1.100:11434 ./ollama-test.sh
```

## Test Types

### Basic Test
- Checks Ollama service availability
- Lists available models
- Sends a simple test request
- Shows performance metrics

### Streaming Test
- Tests real-time streaming responses
- Shows progressive output
- Measures streaming performance

### Minecraft Test
- Tests Minecraft-specific prompts
- Covers mod types, mod loaders, modpacks
- Performance optimization questions
- Conflict detection scenarios

### Temperature Test
- Tests different temperature settings (0.1 to 1.2)
- Shows creativity variation
- Compares response quality

## Configuration

### Environment Variables (Bash Script)
- `OLLAMA_URL`: Ollama base URL (default: `http://localhost:11434`)
- `MODEL`: Model name to test with (default: `codellama:7b`)
- `TEST_TYPE`: Test type to run (default: `basic`)
- `OLLAMA_KEEP_ALIVE`: Keep model loaded duration (default: `30m`)

### Command Line Options (C# Script)
- `--url <url>`: Ollama base URL
- `--model <name>`: Model name to test with
- `--test <type>`: Test type to run
- `--preload`: Preload model before running tests (eliminates first-request delay)
- `--help`: Show help message

## Performance Optimization

### Model Keep-Alive Settings
The scripts now optimize model loading performance by keeping models resident in memory longer:

- **Default**: `OLLAMA_KEEP_ALIVE=30m` (30 minutes)
- **Development**: `OLLAMA_KEEP_ALIVE=30m` (balance memory and convenience)
- **Production**: `OLLAMA_KEEP_ALIVE=1h` or longer
- **Low-memory**: `OLLAMA_KEEP_ALIVE=5m` (current Ollama default)
- **High-traffic**: `OLLAMA_KEEP_ALIVE=-1` (never unload)

### Model Preloading
Use the `--preload` flag to eliminate the initial model loading delay:

```bash
# Bash
./ollama-test.sh --preload --test basic

# C#
dotnet run -- --preload --test basic
```

**Expected Results**:
- First request: ~170s (unavoidable initial load)
- Subsequent requests within keep-alive period: <5s (no model reload)
- Back-to-back tests: Dramatically faster

## Requirements

### C# Script
- .NET 8.0 SDK
- System.Text.Json package (included in project)

### Bash Script
- `curl` (for HTTP requests)
- `jq` (for JSON parsing)
- Bash shell

**Installation:**
```bash
# Ubuntu/Debian
sudo apt-get install curl jq

# macOS
brew install curl jq

# CentOS/RHEL
sudo yum install curl jq
```

## Troubleshooting

### Common Issues

1. **Ollama service not available**
   ```
   ❌ Ollama service is not available at http://localhost:11434
   ```
   **Solution:** Ensure Ollama is running (`ollama serve`)

2. **No models found**
   ```
   ❌ No models found
   ```
   **Solution:** Install models (`ollama pull llamacode:7bn`)

3. **jq not found (Bash script)**
   ```
   ❌ Missing dependencies: jq
   ```
   **Solution:** Install jq package

4. **Model not found**
   ```
   ❌ Model 'llamacode:7bn' not found
   ```
   **Solution:** Pull the model (`ollama pull llamacode:7bn`)

### Debug Mode

For detailed debugging, check Ollama logs:
```bash
# Check Ollama service status
ollama list

# Check Ollama logs
journalctl -u ollama -f

# Test Ollama API directly
curl http://localhost:11434/api/tags
```

## Integration with MCMAA

These test scripts can be used to:

1. **Verify Ollama Setup**: Before running MCMAA, ensure Ollama is working correctly
2. **Test Model Performance**: Find the best model for your use case
3. **Debug Issues**: Isolate problems between Ollama and MCMAA
4. **Performance Testing**: Measure response times and token usage
5. **Prompt Testing**: Test different prompt formats before integrating

## Example Output

### Basic Test Output
```
🧪 Running Basic Ollama Test
═══════════════════════════════
🔍 Checking Ollama availability...
✅ Ollama service is available at http://localhost:11434

📋 Getting available models...
✅ Found 3 models:
   • llamacode:7bn (3.8 GB)
   • phi3:mini (2.3 GB)
   • qwen2.5-coder (4.1 GB)

🧪 Testing with model 'llamacode:7bn'...
🚀 Sending request to model 'llamacode:7bn'...
📝 Prompt: What is Minecraft? Please provide a brief explanation.
⚙️ Temperature: 0.7, Max Tokens: 200

✅ Response received:
Minecraft is a sandbox video game created by Mojang Studios...

📊 Tokens used: 45
⏱️ Duration: 1.23s
```

### Streaming Test Output
```
🧪 Running Streaming Test with 'llamacode:7bn'
═══════════════════════════════════════════
📡 Starting streaming response...
────────────────────────────────────────────────────────
Once upon a time, in a world made of blocks...
────────────────────────────────────────────────────────
✅ Streaming completed. Response length: 1,234 characters
```

This comprehensive testing suite ensures your Ollama setup is working correctly before using it with MCMAA.
