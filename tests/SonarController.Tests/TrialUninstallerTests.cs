using Microsoft.Win32;
using SonarController.Services;
using Xunit;

namespace SonarController.Tests;

public class TrialUninstallerTests
{
    [Fact]
    public void ReturnsNull_WhenNotInstalled()
    {
        Assert.Null(TrialUninstaller.FindUninstallCommand((_, _) => null));
    }

    [Fact]
    public void FindsEntry_InMachineWow6432View()
    {
        var result = TrialUninstaller.FindUninstallCommand((hive, view) =>
            hive == RegistryHive.LocalMachine && view == RegistryView.Registry32 ? "\"C:\\x\\unins000.exe\"" : null);

        Assert.Equal("\"C:\\x\\unins000.exe\"", result);
    }

    [Fact]
    public void PrefersCurrentUser()
    {
        var result = TrialUninstaller.FindUninstallCommand((hive, _) =>
            hive == RegistryHive.CurrentUser ? "user" : "machine");

        Assert.Equal("user", result);
    }
}
