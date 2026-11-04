using System.IO;
using SonarController.Helpers;
using Xunit;

namespace SonarController.Tests;

// AppLogは静的な設定を持つため、他のテストと並列実行しない
[Collection("AppLog")]
public sealed class AppLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SonarControllerTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        AppLog.Initialize();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Write_CreatesLogFile()
    {
        AppLog.Initialize(_dir);

        AppLog.Info("hello");

        var text = File.ReadAllText(Path.Combine(_dir, "app.log"));
        Assert.Contains("[INFO] hello", text);
    }

    [Fact]
    public void Rotation_KeepsOnlyConfiguredNumberOfFiles()
    {
        AppLog.Initialize(_dir, maxFileBytes: 200, maxFiles: 3);

        for (var i = 0; i < 100; i++)
            AppLog.Info(new string('x', 50) + i);

        var files = Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!).OrderBy(n => n).ToArray();
        Assert.Equal(["app.log", "app.log.1", "app.log.2"], files);
    }

    [Fact]
    public void Sanitize_MasksUserProfilePath()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var result = AppLog.Sanitize($"failed to open {profile}\\AppData\\x.json");

        Assert.DoesNotContain(profile, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", result);
    }

    [Fact]
    public void Sanitize_MasksUserName()
    {
        if (Environment.UserName.Length < 3) return;

        var result = AppLog.Sanitize($"owner={Environment.UserName}");

        Assert.DoesNotContain(Environment.UserName, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exception_IsWrittenWithoutPersonalPath()
    {
        AppLog.Initialize(_dir);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        AppLog.Error("boom", new IOException($"cannot read {profile}\\secret.txt"));

        var text = File.ReadAllText(Path.Combine(_dir, "app.log"));
        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
    }
}
