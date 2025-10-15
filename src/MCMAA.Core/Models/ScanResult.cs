namespace MCMAA.Core.Models;

/// <summary>
/// Result of a modpack scan operation
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Path that was scanned
    /// </summary>
    public string ScanPath { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when scan was performed
    /// </summary>
    public DateTime ScanTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total files scanned
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Total directories scanned
    /// </summary>
    public int TotalDirectories { get; set; }

    /// <summary>
    /// Detected mods
    /// </summary>
    public List<ModInfo> Mods { get; set; } = new();

    /// <summary>
    /// Configuration files found
    /// </summary>
    public List<ConfigFile> ConfigFiles { get; set; } = new();

    /// <summary>
    /// Resource packs found
    /// </summary>
    public List<ResourcePack> ResourcePacks { get; set; } = new();

    /// <summary>
    /// Generated markdown report content
    /// </summary>
    public string MarkdownReport { get; set; } = string.Empty;

    /// <summary>
    /// Scan duration
    /// </summary>
    public TimeSpan ScanDuration { get; set; }

    /// <summary>
    /// Any errors encountered during scanning
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warnings generated during scanning
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Information about a detected mod
/// </summary>
public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ModId { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Information about a configuration file
/// </summary>
public class ConfigFile
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

/// <summary>
/// Information about a resource pack
/// </summary>
public class ResourcePack
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}
