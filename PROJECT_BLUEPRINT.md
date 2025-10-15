# 🎮 Minecraft Modpack Config AI Assistant (MCMAA) - Project Blueprint

## 📋 Project Overview

**MCMAA** is an AI-powered assistant designed for Minecraft modpack creators to scan, analyze, and optimize modpack configurations. The system combines intelligent file scanning with local AI analysis to provide comprehensive insights into modpack structure, configuration conflicts, and optimization opportunities.

### 🎯 Core Mission
- **Automate modpack analysis** through intelligent config scanning
- **Provide AI-powered insights** for configuration optimization
- **Detect conflicts** between mods and configurations
- **Generate actionable recommendations** for modpack improvement

---

## 🏗️ Architecture Overview

### High-Level System Design
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   CLI Interface │───▶│  Scanner Module  │───▶│ AI Assistant    │
│   (scanai.py)   │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ Command Utils   │    │ Config Reports   │    │ AI Summaries    │
│ (Arg Parsing)   │    │ (Markdown)       │    │ (Markdown)      │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### 🔧 Technology Stack
- **Language**: Python 3.11+
- **AI Engine**: Ollama (Local LLM execution)
- **Config Parsing**: PyYAML, TOML, JSON5, HJSON
- **File Processing**: Native Python stdlib
- **Caching**: SHA256-based file caching
- **CLI Framework**: argparse

---

## 📁 Project Structure

```
MCMAA-1/
├── 🚀 scanai.py                    # Main CLI entry point
├── 📦 setup.py                     # Package configuration
├── 📋 requirements.txt             # Python dependencies
├── 📖 README.md                    # Project documentation
├── 📊 OPTIMIZATION_GUIDE.md        # Performance optimization guide
│
├── 🛠️ command_utils/               # CLI argument parsing & routing
│   ├── __init__.py
│   ├── main.py                     # Main CLI logic
│   └── paths.py                    # Configuration & path management
│
├── 🔍 scanner/                     # Config file scanner
│   ├── __init__.py
│   ├── config_scanner.py           # Core scanning logic
│   ├── cleanup_output.py           # Output directory management
│   └── output/                     # Generated scan reports
│
├── 🤖 ai_assistant/                # AI integration layer
│   ├── __init__.py
│   ├── core.py                     # Main AI orchestration
│   ├── cache.py                    # Response caching system
│   ├── session_manager.py          # Ollama session pooling
│   ├── preprocessor.py             # Token optimization
│   ├── streaming.py                # Real-time output streaming
│   ├── metrics.py                  # Performance tracking
│   ├── prompts.py                  # AI prompt templates
│   ├── tasks.py                    # Task definitions
│   ├── utils.py                    # Utility functions
│   └── ai_output/                  # Generated AI summaries
│
├── 📊 logs/                        # Performance & debug logs
│   ├── ai_assistant.log
│   ├── metrics_*.json
│   └── performance_metrics.json
│
├── 💾 .cache/                      # AI response cache
└── 🐍 env/                         # Python virtual environment
```

---

## 🔧 Core Components

### 1. 🔍 Scanner Module (`scanner/`)

**Purpose**: Intelligent filesystem scanning for modpack components

**Key Features**:
- **Multi-format support**: TOML, JSON, JSON5, YAML, SNBT, XML, CSV, HJSON, INI
- **Directory structure analysis**: Mods, configs, resourcepacks detection
- **Content preview**: First 10 lines of config files
- **Markdown report generation**: Structured output with syntax highlighting

**Supported File Types**:
```python
LANG_MAP = {
    ".toml": "toml", ".json": "json", ".json5": "json5",
    ".yml": "yaml", ".yaml": "yaml", ".snbt": "txt",
    ".xml": "xml", ".csv": "csv", ".hjson": "hjson",
    ".cfg": "ini", ".ini": "ini", ".properties": "ini"
}
```

### 2. 🤖 AI Assistant Module (`ai_assistant/`)

**Purpose**: Local AI-powered analysis and optimization recommendations

**Core Components**:

#### 🧠 Core Engine (`core.py`)
- **ModpackAI Class**: Main orchestration system
- **Multi-model support**: Lightweight, standard, advanced models
- **Task-based analysis**: Full, quick, conflict, performance analysis
- **Streaming output**: Real-time response generation

#### 💾 Caching System (`cache.py`)
- **SHA256-based keys**: Content + model + temperature hashing
- **Automatic expiration**: 7-day default with configurable cleanup
- **Size management**: 500MB limit with LRU eviction
- **Hit rate tracking**: Performance statistics

#### 🔗 Session Management (`session_manager.py`)
- **Connection pooling**: Persistent Ollama sessions
- **Model warmup**: Reduced latency on first use
- **Retry logic**: Exponential backoff for failed requests
- **Health monitoring**: Session status tracking

#### ⚡ Preprocessing (`preprocessor.py`)
- **Token counting**: Intelligent content optimization
- **Section prioritization**: Mods > Configs > Resourcepacks
- **Smart summarization**: 30% compression ratio
- **Content filtering**: Remove redundant information

#### 📡 Streaming (`streaming.py`)
- **Real-time output**: Progressive file writing
- **Progress indicators**: Visual feedback during analysis
- **Chunk processing**: Efficient memory usage
- **Error recovery**: Graceful failure handling

### 3. 🛠️ Command Utils (`command_utils/`)

**Purpose**: CLI interface and configuration management

**Key Features**:
- **Argument parsing**: Comprehensive CLI options
- **Model selection**: Task-specific model routing
- **Configuration management**: Centralized settings
- **Path resolution**: Cross-platform compatibility

---

## ⚙️ Configuration System

### 🎛️ Model Configuration
```python
AI_CONFIG = {
    "models": {
        "lightweight": "phi3:mini",      # Fast summaries
        "standard": "qwen2.5-coder",     # Balanced analysis  
        "advanced": "qwen2.5:14b",       # Complex analysis
    },
    "task_models": {
        "summary": "lightweight",
        "quick": "lightweight", 
        "conflicts": "standard",
        "performance": "standard",
        "full": "standard"
    }
}
```

### 💾 Cache Configuration
```python
CACHE_CONFIG = {
    "enabled": True,
    "max_size_mb": 500,
    "expiry_days": 7,
    "hash_algorithm": "sha256"
}
```

### ⏱️ Timeout Configuration
```python
TIMEOUT_CONFIG = {
    "request_standard": 300,    # 5 minutes
    "request_large": 600,       # 10 minutes
    "request_complex": 900,     # 15 minutes
    "max_retries": 2
}
```

---

## 🚀 Usage Workflow

### Basic Analysis
```bash
scanai /path/to/modpack                    # Full analysis
scanai /path/to/modpack --task quick       # Quick summary
scanai /path/to/modpack --no-cache         # Skip cache
scanai /path/to/modpack --model phi3:mini  # Specific model
```

### Advanced Options
```bash
scanai /path/to/modpack --no-streaming     # Disable streaming
scanai /path/to/modpack --clear-cache      # Clear cache first
scanai /path/to/modpack --stats            # Show performance stats
```

### Processing Pipeline
1. **📁 Directory Scan**: Identify mods, configs, resourcepacks
2. **📝 Report Generation**: Create structured Markdown report
3. **🤖 AI Analysis**: Process report with selected model
4. **💾 Caching**: Store results for future use
5. **📊 Metrics**: Track performance statistics

---

## 🎯 Key Features

### ✅ Implemented Features
- ✅ **Multi-format config scanning** (TOML, JSON, YAML, SNBT, XML, etc.)
- ✅ **AI-powered analysis** via Ollama local LLMs
- ✅ **Response caching** with SHA256 hashing
- ✅ **Session pooling** for Ollama connections
- ✅ **Streaming output** with progress indicators
- ✅ **Token-aware preprocessing** for optimal AI input
- ✅ **Performance metrics** and logging
- ✅ **CLI with multiple options** (task types, models, caching)

### 🔄 Planned Features (Roadmap)
- 🔄 **Conflict detector** (biomes, mobs, ores)
- 🔄 **Config preset system** (RPG mode, Hardcore survival)
- 🔄 **Resourcepack generator** (custom menus, sounds)
- 🔄 **VS Code integration** (extension or terminal panel)
- 🔄 **Template system** (save/load config styles)
- 🔄 **Export tool** (build distributable modpack zip)

---

## 📊 Performance Optimizations

### 🚀 Speed Improvements
- **Streaming output**: Real-time response generation
- **Intelligent caching**: Avoid redundant AI calls
- **Session pooling**: Persistent model connections
- **Smart preprocessing**: Optimize token usage
- **Model selection**: Task-appropriate model routing

### 📈 Metrics Tracking
- **Cache hit rates**: Monitor caching effectiveness
- **Request duration**: Track processing times
- **Token usage**: Optimize AI input costs
- **Error rates**: Monitor system reliability

---

## 🔮 Future Roadmap

### Phase 1: Core Stability ✅
- [x] Basic scanning functionality
- [x] AI integration with Ollama
- [x] Performance optimizations
- [x] Caching system

### Phase 2: Advanced Analysis 🔄
- [ ] Conflict detection algorithms
- [ ] Performance bottleneck identification
- [ ] Mod compatibility analysis
- [ ] Resource usage optimization

### Phase 3: User Experience 🔄
- [ ] VS Code extension
- [ ] Web-based GUI (optional)
- [ ] Interactive configuration editor
- [ ] Preset management system

### Phase 4: Automation 🔄
- [ ] Automated modpack building
- [ ] CI/CD integration
- [ ] Batch processing capabilities
- [ ] Export/distribution tools

---

## 🛠️ Development Notes

### 🔧 Technical Debt
- **Testing**: No formal test suite implemented yet
- **Documentation**: API documentation needs expansion
- **Error Handling**: Some edge cases need better coverage
- **Configuration**: More flexible configuration options needed

### 🎯 Optimization Opportunities
- **Parallel processing**: Multi-threaded scanning
- **Database integration**: Persistent metadata storage
- **Plugin system**: Extensible analysis modules
- **Cloud integration**: Optional cloud AI models

---

*This blueprint serves as the comprehensive guide for understanding, maintaining, and extending the MCMAA project.*
