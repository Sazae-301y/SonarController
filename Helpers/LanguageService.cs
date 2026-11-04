using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SonarController.Helpers;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed class LanguageService : ObservableObject
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonarController",
        "language-settings.json");

    private ResourceDictionary _resources;
    private string _currentLanguage = "ja";
    private string? _loadErrorDetail;

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("ja", "日本語"),
        new("en", "English"),
        new("zh-CN", "简体中文")
    ];

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set => SetProperty(ref _currentLanguage, value);
    }

    public string? LoadError { get; private set; }

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
        _resources = new ResourceDictionary();
        LoadSavedLanguage();
        ApplyResources(_currentLanguage);
        if (_loadErrorDetail is not null)
            LoadError = string.Format(GetString("LanguageLoadError"), _loadErrorDetail);
    }

    public string GetString(string key) =>
        _resources.Contains(key) ? _resources[key]?.ToString() ?? key : key;

    public bool TrySetLanguage(string language, out string? error)
    {
        if (Languages.All(option => option.Code != language))
        {
            error = GetString("LanguageInvalid");
            return false;
        }

        if (language == CurrentLanguage)
        {
            error = null;
            return true;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new LanguageSettings(language)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = string.Format(GetString("LanguageSaveError"), exception.Message);
            return false;
        }

        ApplyResources(language);
        _loadErrorDetail = null;
        LoadError = null;
        error = null;
        return true;
    }

    private void LoadSavedLanguage()
    {
        if (!File.Exists(SettingsPath)) return;

        try
        {
            var saved = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(SettingsPath));
            if (saved is not null && Languages.Any(option => option.Code == saved.Language))
                _currentLanguage = saved.Language;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _loadErrorDetail = exception.Message;
        }
    }

    private void ApplyResources(string language)
    {
        var uri = new Uri($"pack://application:,,,/Resources/Strings.{language}.xaml");
        var dictionary = new ResourceDictionary { Source = uri };
        var mergedDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var previousIndex = mergedDictionaries.IndexOf(_resources);
        if (previousIndex >= 0)
            mergedDictionaries[previousIndex] = dictionary;
        else
            mergedDictionaries.Add(dictionary);

        _resources = dictionary;
        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record LanguageSettings(string Language);
}
