using MCMAA.Core.Models;

namespace MCMAA.Tests;

public class BasicModelTests
{
    [Fact]
    public void ScanResult_ShouldInitializeWithDefaults()
    {
        // Act
        var scanResult = new ScanResult();

        // Assert
        Assert.NotNull(scanResult.ScanPath);
        Assert.NotNull(scanResult.Mods);
        Assert.NotNull(scanResult.ConfigFiles);
        Assert.NotNull(scanResult.ResourcePacks);
        Assert.NotNull(scanResult.Errors);
        Assert.NotNull(scanResult.Warnings);
        Assert.Empty(scanResult.Mods);
        Assert.Empty(scanResult.ConfigFiles);
        Assert.Empty(scanResult.ResourcePacks);
    }

    [Fact]
    public void ModInfo_ShouldInitializeWithDefaults()
    {
        // Act
        var modInfo = new ModInfo();

        // Assert
        Assert.NotNull(modInfo.Name);
        Assert.NotNull(modInfo.Version);
        Assert.NotNull(modInfo.FilePath);
        Assert.NotNull(modInfo.ModId);
        Assert.Equal(string.Empty, modInfo.Name);
        Assert.Equal(0, modInfo.FileSize);
    }

    [Fact]
    public void ConfigFile_ShouldInitializeWithDefaults()
    {
        // Act
        var configFile = new ConfigFile();

        // Assert
        Assert.NotNull(configFile.Name);
        Assert.NotNull(configFile.FilePath);
        Assert.NotNull(configFile.FileType);
        Assert.NotNull(configFile.Preview);
        Assert.NotNull(configFile.Language);
        Assert.Equal(string.Empty, configFile.Name);
        Assert.Equal(0, configFile.FileSize);
    }

    [Fact]
    public void ResourcePack_ShouldInitializeWithDefaults()
    {
        // Act
        var resourcePack = new ResourcePack();

        // Assert
        Assert.NotNull(resourcePack.Name);
        Assert.NotNull(resourcePack.FilePath);
        Assert.NotNull(resourcePack.Description);
        Assert.Equal(string.Empty, resourcePack.Name);
        Assert.Equal(0, resourcePack.FileSize);
    }

    [Fact]
    public void ScanResult_ShouldAllowSettingProperties()
    {
        // Arrange
        var scanResult = new ScanResult();
        var testMod = new ModInfo { Name = "TestMod", Version = "1.0.0" };
        var testConfig = new ConfigFile { Name = "test.toml", FileType = "TOML" };

        // Act
        scanResult.ScanPath = "/test/path";
        scanResult.TotalFiles = 100;
        scanResult.TotalDirectories = 10;
        scanResult.Mods.Add(testMod);
        scanResult.ConfigFiles.Add(testConfig);

        // Assert
        Assert.Equal("/test/path", scanResult.ScanPath);
        Assert.Equal(100, scanResult.TotalFiles);
        Assert.Equal(10, scanResult.TotalDirectories);
        Assert.Single(scanResult.Mods);
        Assert.Single(scanResult.ConfigFiles);
        Assert.Equal("TestMod", scanResult.Mods[0].Name);
        Assert.Equal("test.toml", scanResult.ConfigFiles[0].Name);
    }

    [Fact]
    public void AnalysisTask_ShouldInitializeWithDefaults()
    {
        // Act
        var task = new AnalysisTask();

        // Assert
        Assert.NotNull(task.Name);
        Assert.NotNull(task.Description);
        Assert.NotNull(task.RecommendedModel);
        Assert.Equal(string.Empty, task.Name);
        Assert.Equal(string.Empty, task.Description);
        Assert.Equal(string.Empty, task.RecommendedModel);
        Assert.Equal(AnalysisTaskType.Full, task.Type);
        Assert.Equal(TimeoutCategory.Standard, task.TimeoutCategory);
        Assert.Equal(5, task.Priority);
    }

    [Fact]
    public void AiAnalysisResult_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new AiAnalysisResult();

        // Assert
        Assert.NotNull(result.Content);
        Assert.NotNull(result.ModelUsed);
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
        Assert.NotNull(result.Task);
        Assert.Equal(string.Empty, result.Content);
        Assert.Equal(string.Empty, result.ModelUsed);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.True(result.Success); // Default is true
        Assert.False(result.FromCache);
        Assert.False(result.StreamingUsed);
        Assert.Equal(0, result.TokensUsed);
        Assert.Equal(0.0, result.Temperature);
    }
}