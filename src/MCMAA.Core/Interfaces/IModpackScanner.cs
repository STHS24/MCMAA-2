using MCMAA.Core.Models;

namespace MCMAA.Core.Interfaces;

/// <summary>
/// Interface for modpack scanning functionality
/// </summary>
public interface IModpackScanner
{
    /// <summary>
    /// Scans a modpack directory and generates a report
    /// </summary>
    /// <param name="path">Path to the modpack directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scan result with detected files and generated report</returns>
    Task<ScanResult> ScanAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a path contains a valid modpack structure
    /// </summary>
    /// <param name="path">Path to validate</param>
    /// <returns>True if path appears to be a valid modpack</returns>
    bool IsValidModpackPath(string path);

    /// <summary>
    /// Gets supported file extensions for configuration files
    /// </summary>
    /// <returns>Dictionary mapping extensions to language identifiers</returns>
    Dictionary<string, string> GetSupportedExtensions();
}
