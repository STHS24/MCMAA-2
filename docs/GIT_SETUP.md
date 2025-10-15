# Git Configuration for MCMAA

This document explains the Git configuration setup for the MCMAA project.

## Files Overview

### `.gitignore`
A comprehensive .gitignore file that covers:

#### .NET Build Artifacts
- `bin/` and `obj/` directories
- Build outputs (`Debug/`, `Release/`)
- Visual Studio cache files (`.vs/`)
- Test results and coverage reports
- NuGet package cache files
- MSBuild logs and temporary files

#### IDE and Editor Files
- Visual Studio files (`.vs/`, `*.user`, `*.suo`)
- Visual Studio Code settings (`.vscode/`)
- JetBrains Rider files (`.idea/`, `*.sln.iml`)
- ReSharper cache files

#### MCMAA Application Specific
- Runtime directories (`cache/`, `logs/`, `output/`)
- Configuration files with sensitive data (`appsettings.local.json`)
- AI model files (`*.gguf`, `*.bin`, `*.safetensors`)
- Large data files and archives
- Metrics and analytics exports
- Test data and fixtures

#### Operating System Files
- **Windows**: `Thumbs.db`, `Desktop.ini`, `$RECYCLE.BIN/`, `*:Zone.Identifier`
- **macOS**: `.DS_Store`, `.AppleDouble`, `.Spotlight-V100`
- **Linux**: `*~`, `.directory`, `.Trash-*`

#### Development Tools
- Package manager files (`packages/`, `node_modules/`)
- Container files (`Dockerfile.local`, `docker-compose.override.yml`)
- Cloud deployment files (`.azure/`, `.aws/`, `.gcp/`)
- Environment files (`.env`, `.env.local`)
- Security scanning results (`*.sarif`)

### `.gitattributes`
Ensures consistent file handling across platforms:

#### Text File Normalization
- Auto-detection of text files with LF normalization
- Explicit text file declarations for `.cs`, `.json`, `.xml`, etc.
- Platform-specific line endings (CRLF for `.bat`, LF for `.sh`)

#### Binary File Handling
- Proper binary file detection for images, executables, archives
- .NET specific binaries (`.dll`, `.exe`, `.snk`, `.pfx`)
- Minecraft specific files (`.jar` as binary, `.mcmeta` as text)

#### Language Detection
- C# language detection for GitHub Linguist
- Export ignore patterns for documentation and test files

## Usage Guidelines

### Adding New File Types
When adding support for new file formats:

1. **Text files**: Add to `.gitattributes` with `text` attribute
2. **Binary files**: Add to `.gitattributes` with `binary` attribute
3. **Generated files**: Add to `.gitignore` if they shouldn't be tracked
4. **Configuration files**: Consider if they contain sensitive data

### Sensitive Data
Never commit:
- API keys or tokens
- Database connection strings with passwords
- Local configuration overrides
- Personal development settings

Use patterns like:
- `appsettings.local.json`
- `*.secrets.json`
- `.env.local`

### Large Files
For files larger than 100MB, consider:
- Adding to `.gitignore` if they're generated
- Using Git LFS if they need to be tracked
- Storing in external storage (cloud, CDN)

### Build Artifacts
The following are automatically ignored:
- All `bin/` and `obj/` directories
- NuGet package cache
- Test results and coverage reports
- Visual Studio temporary files

## Maintenance

### Regular Cleanup
Periodically run:
```bash
# Remove untracked files (dry run first)
git clean -n
git clean -f

# Remove ignored files
git clean -fX
```

### Updating Patterns
When adding new project types or tools:
1. Update `.gitignore` with new patterns
2. Update `.gitattributes` for new file types
3. Test with `git status --ignored` to verify
4. Document changes in this file

## Verification

To verify the configuration is working:

```bash
# Check ignored files
git status --ignored --porcelain

# Check file attributes
git check-attr -a filename

# Test line ending handling
git ls-files --eol
```

## Future Considerations

The current setup is designed to be future-proof and includes patterns for:
- Container deployment (Docker, Kubernetes)
- Cloud platforms (Azure, AWS, GCP)
- Additional .NET project types
- Modern development tools
- Security scanning tools
- Performance profiling tools

This ensures the repository remains clean and professional as the project evolves.
