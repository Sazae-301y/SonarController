using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SonarController.Helpers;

public sealed partial class InteractionModeService : ObservableObject
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController",
        "interaction-settings.json");

    private bool _isLoading;

    [ObservableProperty]
    private bool _isTouchOptimized;

    [ObservableProperty]
    private string _status = string.Empty;

    public InteractionModeService()
    {
        if (!File.Exists(SettingsPath)) return;

        try
        {
            var settings = JsonSerializer.Deserialize<InteractionSettings>(File.ReadAllText(SettingsPath));
            _isLoading = true;
            IsTouchOptimized = settings?.IsTouchOptimized ?? false;
            _isLoading = false;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _isLoading = false;
            Status = $"操作モードの設定を読み込めませんでした: {exception.Message}";
        }
    }

    partial void OnIsTouchOptimizedChanged(bool value)
    {
        if (_isLoading) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new InteractionSettings(value));
            File.WriteAllText(SettingsPath, json);
            Status = "操作モードを保存しました。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _isLoading = true;
            IsTouchOptimized = !value;
            _isLoading = false;
            Status = $"操作モードを保存できませんでした: {exception.Message}";
        }
    }

    private sealed record InteractionSettings(bool IsTouchOptimized);
}
