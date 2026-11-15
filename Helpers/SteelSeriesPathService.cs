using System.IO;
using System.Text.Json;

namespace SonarController.Helpers;

public enum SteelSeriesPathSaveResult
{
    Success,
    InvalidPath,
    SaveFailed
}

public sealed class SteelSeriesPathService
{
    private static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController",
        "steelseries-settings.json");

    private readonly string _settingsPath;

    public string? ExecutablePath { get; private set; }
    public string? LoadError { get; }
    public event EventHandler? ExecutablePathChanged;

    public SteelSeriesPathService() : this(DefaultSettingsPath)
    {
    }

    internal static string GetSuggestedExecutablePath()
    {
        var candidates = GetDefaultExecutablePaths();
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    internal static string[] GetDefaultExecutablePaths() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SteelSeries", "GG", "SteelSeriesGG.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SteelSeries", "GG", "SteelSeriesGG.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelSeries", "GG", "SteelSeriesGG.exe")
    ];

    internal SteelSeriesPathService(string settingsPath)
    {
        _settingsPath = settingsPath;
        if (!File.Exists(_settingsPath)) return;

        try
        {
            ExecutablePath = JsonSerializer.Deserialize<SteelSeriesSettings>(
                File.ReadAllText(_settingsPath))?.ExecutablePath;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadError = exception.Message;
        }
    }

    public SteelSeriesPathSaveResult TrySetExecutablePath(string? path, out string? error)
    {
        string? normalizedPath = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                normalizedPath = Path.GetFullPath(path.Trim());
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = exception.Message;
                return SteelSeriesPathSaveResult.InvalidPath;
            }

            if (!File.Exists(normalizedPath) ||
                !string.Equals(Path.GetFileName(normalizedPath), "SteelSeriesGG.exe", StringComparison.OrdinalIgnoreCase))
            {
                error = null;
                return SteelSeriesPathSaveResult.InvalidPath;
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new SteelSeriesSettings(normalizedPath)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return SteelSeriesPathSaveResult.SaveFailed;
        }

        ExecutablePath = normalizedPath;
        error = null;
        ExecutablePathChanged?.Invoke(this, EventArgs.Empty);
        return SteelSeriesPathSaveResult.Success;
    }

    private sealed record SteelSeriesSettings(string? ExecutablePath);
}
