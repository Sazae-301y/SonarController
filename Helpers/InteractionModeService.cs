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
    private readonly LanguageService _language;
    private string? _statusKey;
    private string? _statusDetail;

    [ObservableProperty]
    private bool _isTouchOptimized;

    [ObservableProperty]
    private string _status = string.Empty;

    public InteractionModeService(LanguageService language)
    {
        _language = language;
        _language.LanguageChanged += (_, _) => UpdateStatus();
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
            SetStatus("TouchLoadError", exception.Message);
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
            SetStatus("TouchSaved");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _isLoading = true;
            IsTouchOptimized = !value;
            _isLoading = false;
            SetStatus("TouchSaveError", exception.Message);
        }
    }

    private void SetStatus(string key, string? detail = null)
    {
        _statusKey = key;
        _statusDetail = detail;
        UpdateStatus();
    }

    private void UpdateStatus() =>
        Status = _statusKey is null
            ? string.Empty
            : _statusDetail is null
                ? _language.GetString(_statusKey)
                : string.Format(_language.GetString(_statusKey), _statusDetail);

    private sealed record InteractionSettings(bool IsTouchOptimized);
}
