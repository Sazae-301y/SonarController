using System;
using System.IO;
using SonarController.Helpers;
using Xunit;

namespace SonarController.Tests;

public class SteelSeriesPathServiceTests
{
    [Fact]
    public void SavesAndReloadsExecutablePathAndCanRestoreDefaultSearch()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var executablePath = Path.Combine(directory, "SteelSeriesGG.exe");
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(executablePath, string.Empty);
            var service = new SteelSeriesPathService(settingsPath);

            Assert.Equal(SteelSeriesPathSaveResult.Success,
                service.TrySetExecutablePath(executablePath, out var error));
            Assert.Null(error);
            Assert.Equal(Path.GetFullPath(executablePath), new SteelSeriesPathService(settingsPath).ExecutablePath);

            Assert.Equal(SteelSeriesPathSaveResult.Success,
                service.TrySetExecutablePath(" ", out error));
            Assert.Null(error);
            Assert.Null(new SteelSeriesPathService(settingsPath).ExecutablePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsMissingOrWrongExecutableWithoutReplacingSavedPath()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var executablePath = Path.Combine(directory, "SteelSeriesGG.exe");
            File.WriteAllText(executablePath, string.Empty);
            var service = new SteelSeriesPathService(Path.Combine(directory, "settings.json"));
            Assert.Equal(SteelSeriesPathSaveResult.Success,
                service.TrySetExecutablePath(executablePath, out _));

            Assert.Equal(SteelSeriesPathSaveResult.InvalidPath,
                service.TrySetExecutablePath(Path.Combine(directory, "missing.exe"), out _));
            Assert.Equal(SteelSeriesPathSaveResult.InvalidPath,
                service.TrySetExecutablePath(executablePath.Replace("SteelSeriesGG.exe", "other.exe"), out _));
            Assert.Equal(Path.GetFullPath(executablePath), service.ExecutablePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SonarController.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
