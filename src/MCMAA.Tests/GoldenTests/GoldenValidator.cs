using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace MCMAA.Tests.GoldenTests
{
    /// <summary>
    /// Golden validation harness.
    /// - Reads tests/Golden/manifest.json
    /// - Executes the CLI in deterministic mode (temperature=0) for smoke samples
    /// - Normalizes outputs and compares against golden outputs
    /// 
    /// Notes:
    /// - This test runs the CLI as a subprocess (dotnet run --project src/MCMAA.CLI)
    /// - CI must have the .NET 8 SDK available and Ollama/local-models should be configured if required
    /// - Smoke samples are selected by "priority": "smoke" in the manifest
    /// </summary>
    public class GoldenValidator
    {
        private const string ManifestPath = "tests/Golden/manifest.json";
        private const string OutputPath = "output/last_run.json";

        [Fact]
        public async Task SmokeSamples_ShouldNotRegress()
        {
            var manifestJson = await File.ReadAllTextAsync(ManifestPath);
            using var manifestDoc = JsonDocument.Parse(manifestJson);
            var samples = manifestDoc.RootElement.GetProperty("samples")
                .EnumerateArray()
                .Where(s => s.GetProperty("priority").GetString() == "smoke");

            foreach (var sample in samples)
            {
                var id = sample.GetProperty("id").GetString();
                var path = sample.GetProperty("path").GetString();
                var tasks = sample.GetProperty("tasks").EnumerateArray().Select(t => t.GetString()).ToArray();

                foreach (var task in tasks)
                {
                    // Run CLI for the sample in deterministic mode
                    var exit = await RunCliAnalyze(path, task);
                    exit.Should().Be(0, because: $"CLI failed for sample {id} task {task}");

                    // Expect the CLI to write to output/last_run.json
                    if (!File.Exists(OutputPath))
                    {
                        throw new FileNotFoundException($"Expected output file not found at {OutputPath}");
                    }

                    var actual = await File.ReadAllTextAsync(OutputPath);
                    var goldenFile = Path.Combine(path, $"golden_{{task.ToLower()}}.json");

                    if (!File.Exists(goldenFile))
                    {
                        // If no golden exists for this task, fail the test and instruct to add golden
                        throw new FileNotFoundException($"Golden file not found: {{goldenFile}}. Please add the golden output for sample {{id}} task {{task}}");
                    }

                    var expected = await File.ReadAllTextAsync(goldenFile);

                    var normalizedActual = GoldenNormalization.NormalizeJson(actual);
                    var normalizedExpected = GoldenNormalization.NormalizeJson(expected);

                    // If the output is structured JSON, compare canonicalized JSON strings
                    normalizedActual.Should().Be(normalizedExpected, because: $"Golden mismatch for sample {{id}} task {{task}}");
                }
            }
        }

        private async Task<int> RunCliAnalyze(string samplePath, string task)
        {
            // Build the argument set for deterministic evaluation:
            // - temperature=0
            // - fixed model if "deterministic" available; otherwise set temperature to 0 to reduce variance
            // - output path to output/last_run.json
            var args = $"--project src/MCMAA.CLI -- analyze \"{{samplePath}}\" --task {{task}} --temperature 0 --output \"{{Path.GetFullPath(OutputPath)}}\"";

            var psi = new ProcessStartInfo("dotnet", $"run {{args}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Failed to start dotnet process");

            // capture output for diagnostics
            var stdOut = await process.StandardOutput.ReadToEndAsync();
            var stdErr = await process.StandardError.ReadToEndAsync();

            // Wait for exit
            await process.WaitForExitAsync();

            // Write logs for CI visibility
            var logDir = Path.Combine("output","golden-logs");
            Directory.CreateDirectory(logDir);
            var idSafe = Path.GetFileName(samplePath).Replace(Path.DirectorySeparatorChar, '_').Replace(":", "_");
            await File.WriteAllTextAsync(Path.Combine(logDir, $"{{idSafe}}_{{task}}_stdout.log"), stdOut);
            await File.WriteAllTextAsync(Path.Combine(logDir, $"{{idSafe}}_{{task}}_stderr.log"), stdErr);

            return process.ExitCode;
        }
    }
}