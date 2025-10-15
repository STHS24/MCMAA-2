using MCMAA.Core.Interfaces;
using MCMAA.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using Tomlyn;
using CsvHelper;
using System.Globalization;
using System.Xml;

namespace MCMAA.Scanner;

/// <summary>
/// Implementation of modpack scanning functionality
/// </summary>
public class ModpackScanner : IModpackScanner
{
    private readonly ILogger<ModpackScanner> _logger;

    /// <summary>
    /// Supported file extensions and their language mappings
    /// </summary>
    private static readonly Dictionary<string, string> SupportedExtensions = new()
    {
        [".toml"] = "toml",
        [".json"] = "json",
        [".json5"] = "json5",
        [".yml"] = "yaml",
        [".yaml"] = "yaml",
        [".snbt"] = "txt",
        [".xml"] = "xml",
        [".csv"] = "csv",
        [".hjson"] = "hjson",
        [".cfg"] = "ini",
        [".ini"] = "ini",
        [".properties"] = "ini"
    };

    public ModpackScanner(ILogger<ModpackScanner> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string> GetSupportedExtensions() => SupportedExtensions;

    public bool IsValidModpackPath(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // Check for common modpack indicators
        var indicators = new[]
        {
            "mods", "config", "resourcepacks", "shaderpacks",
            "saves", "scripts", "kubejs", "defaultconfigs"
        };

        return indicators.Any(indicator =>
            Directory.Exists(Path.Combine(path, indicator)) ||
            File.Exists(Path.Combine(path, indicator)));
    }

    public async Task<ScanResult> ScanAsync(string path, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting modpack scan of: {Path}", path);

        var result = new ScanResult
        {
            ScanPath = path,
            ScanTimestamp = startTime
        };

        try
        {
            if (!IsValidModpackPath(path))
            {
                result.Errors.Add($"Path does not appear to be a valid modpack: {path}");
                return result;
            }

            // Scan for mods
            await ScanModsAsync(path, result, cancellationToken);

            // Scan for config files
            await ScanConfigFilesAsync(path, result, cancellationToken);

            // Scan for resource packs
            await ScanResourcePacksAsync(path, result, cancellationToken);

            // Generate markdown report
            result.MarkdownReport = GenerateMarkdownReport(result);

            result.ScanDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Scan completed in {Duration}ms", result.ScanDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during modpack scan");
            result.Errors.Add($"Scan failed: {ex.Message}");
        }

        return result;
    }

    private async Task ScanModsAsync(string basePath, ScanResult result, CancellationToken cancellationToken)
    {
        var modsPath = Path.Combine(basePath, "mods");
        if (!Directory.Exists(modsPath))
            return;

        _logger.LogDebug("Scanning mods directory: {ModsPath}", modsPath);

        var modFiles = Directory.GetFiles(modsPath, "*.jar", SearchOption.AllDirectories);

        foreach (var modFile in modFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var fileInfo = new FileInfo(modFile);
                var modInfo = new ModInfo
                {
                    Name = Path.GetFileNameWithoutExtension(modFile),
                    FilePath = Path.GetRelativePath(basePath, modFile),
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Version = "Unknown" // Could be extracted from mod metadata if needed
                };

                result.Mods.Add(modInfo);
                result.TotalFiles++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing mod file: {ModFile}", modFile);
                result.Warnings.Add($"Could not process mod file: {Path.GetFileName(modFile)}");
            }
        }

        _logger.LogDebug("Found {ModCount} mods", result.Mods.Count);
    }

    private async Task ScanConfigFilesAsync(string basePath, ScanResult result, CancellationToken cancellationToken)
    {
        var configPaths = new[] { "config", "defaultconfigs", "scripts", "kubejs" };

        foreach (var configDir in configPaths)
        {
            var configPath = Path.Combine(basePath, configDir);
            if (!Directory.Exists(configPath))
                continue;

            _logger.LogDebug("Scanning config directory: {ConfigPath}", configPath);
            await ScanConfigDirectoryAsync(basePath, configPath, result, cancellationToken);
        }

        _logger.LogDebug("Found {ConfigCount} config files", result.ConfigFiles.Count);
    }

    private async Task ScanConfigDirectoryAsync(string basePath, string configPath, ScanResult result, CancellationToken cancellationToken)
    {
        var allFiles = Directory.GetFiles(configPath, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (!SupportedExtensions.ContainsKey(extension))
                continue;

            try
            {
                var fileInfo = new FileInfo(file);
                var preview = await GetFilePreviewAsync(file, 10);

                var configFile = new ConfigFile
                {
                    Name = Path.GetFileName(file),
                    FilePath = Path.GetRelativePath(basePath, file),
                    FileType = extension,
                    Language = SupportedExtensions[extension],
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Preview = preview
                };

                result.ConfigFiles.Add(configFile);
                result.TotalFiles++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing config file: {File}", file);
                result.Warnings.Add($"Could not process config file: {Path.GetFileName(file)}");
            }
        }
    }

    private async Task ScanResourcePacksAsync(string basePath, ScanResult result, CancellationToken cancellationToken)
    {
        var resourcePackPaths = new[] { "resourcepacks", "shaderpacks" };

        foreach (var packDir in resourcePackPaths)
        {
            var packPath = Path.Combine(basePath, packDir);
            if (!Directory.Exists(packPath))
                continue;

            _logger.LogDebug("Scanning resource pack directory: {PackPath}", packPath);

            var packFiles = Directory.GetFiles(packPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".zip") || Directory.Exists(f));

            foreach (var packFile in packFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var isDirectory = Directory.Exists(packFile);
                    var fileInfo = isDirectory ? null : new FileInfo(packFile);

                    var resourcePack = new ResourcePack
                    {
                        Name = Path.GetFileName(packFile),
                        FilePath = Path.GetRelativePath(basePath, packFile),
                        FileSize = fileInfo?.Length ?? 0,
                        LastModified = fileInfo?.LastWriteTime ?? Directory.GetLastWriteTime(packFile),
                        Description = "Resource pack" // Could be extracted from pack.mcmeta if needed
                    };

                    result.ResourcePacks.Add(resourcePack);
                    if (!isDirectory) result.TotalFiles++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing resource pack: {PackFile}", packFile);
                    result.Warnings.Add($"Could not process resource pack: {Path.GetFileName(packFile)}");
                }
            }
        }

        _logger.LogDebug("Found {PackCount} resource packs", result.ResourcePacks.Count);
    }

    private async Task<string> GetFilePreviewAsync(string filePath, int maxLines)
    {
        try
        {
            var lines = new List<string>();
            using var reader = new StreamReader(filePath);

            for (int i = 0; i < maxLines && !reader.EndOfStream; i++)
            {
                var line = await reader.ReadLineAsync();
                if (line != null)
                    lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read file preview: {FilePath}", filePath);
            return "[Preview unavailable]";
        }
    }

    private string GenerateMarkdownReport(ScanResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Modpack Analysis Report");
        sb.AppendLine($"**Scan Path:** `{result.ScanPath}`");
        sb.AppendLine($"**Scan Date:** {result.ScanTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Scan Duration:** {result.ScanDuration.TotalSeconds:F2} seconds");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## 📊 Summary");
        sb.AppendLine($"- **Total Files:** {result.TotalFiles}");
        sb.AppendLine($"- **Mods:** {result.Mods.Count}");
        sb.AppendLine($"- **Config Files:** {result.ConfigFiles.Count}");
        sb.AppendLine($"- **Resource Packs:** {result.ResourcePacks.Count}");
        sb.AppendLine();

        // Mods section
        if (result.Mods.Any())
        {
            sb.AppendLine("## 🔧 Mods");
            sb.AppendLine("| Name | Version | File Size | Last Modified |");
            sb.AppendLine("|------|---------|-----------|---------------|");

            foreach (var mod in result.Mods.OrderBy(m => m.Name))
            {
                sb.AppendLine($"| {mod.Name} | {mod.Version} | {FormatFileSize(mod.FileSize)} | {mod.LastModified:yyyy-MM-dd} |");
            }
            sb.AppendLine();
        }

        // Config files section
        if (result.ConfigFiles.Any())
        {
            sb.AppendLine("## ⚙️ Configuration Files");

            var configGroups = result.ConfigFiles.GroupBy(c => c.Language);
            foreach (var group in configGroups.OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {group.Key.ToUpperInvariant()} Files");

                foreach (var config in group.OrderBy(c => c.FilePath))
                {
                    sb.AppendLine($"#### `{config.FilePath}`");
                    sb.AppendLine($"**Size:** {FormatFileSize(config.FileSize)} | **Modified:** {config.LastModified:yyyy-MM-dd}");
                    sb.AppendLine();
                    sb.AppendLine($"```{config.Language}");
                    sb.AppendLine(config.Preview);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        // Resource packs section
        if (result.ResourcePacks.Any())
        {
            sb.AppendLine("## 🎨 Resource Packs");
            sb.AppendLine("| Name | Description | File Size | Last Modified |");
            sb.AppendLine("|------|-------------|-----------|---------------|");

            foreach (var pack in result.ResourcePacks.OrderBy(p => p.Name))
            {
                sb.AppendLine($"| {pack.Name} | {pack.Description} | {FormatFileSize(pack.FileSize)} | {pack.LastModified:yyyy-MM-dd} |");
            }
            sb.AppendLine();
        }

        // Warnings and errors
        if (result.Warnings.Any() || result.Errors.Any())
        {
            sb.AppendLine("## ⚠️ Issues");

            if (result.Errors.Any())
            {
                sb.AppendLine("### Errors");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"- ❌ {error}");
                }
                sb.AppendLine();
            }

            if (result.Warnings.Any())
            {
                sb.AppendLine("### Warnings");
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"- ⚠️ {warning}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
