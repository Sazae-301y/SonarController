using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SonarController.Helpers;
using SonarController.Models;
using SonarController.Services;
using SonarController.Services.License;
using System.Windows.Input;

namespace SonarController.ViewModels;

/// <summary>
/// 設定ウィンドウ用 ViewModel。自動起動設定とバージョン情報を扱う。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private bool _suppressEvents;

    [ObservableProperty]
    private bool _isAutoStartEnabled;

    [ObservableProperty]
    private string _startupStatus = string.Empty;

    [ObservableProperty]
    private bool _isCapturingHotkey;

    [ObservableProperty]
    private string _hotkeyDisplay;

    [ObservableProperty]
    private string _hotkeyStatus = string.Empty;

    [ObservableProperty]
    private DisplayOption? _selectedDisplay;

    [ObservableProperty]
    private string _displayStatus = string.Empty;

    [ObservableProperty]
    private string _selectedSteelSeriesExecutablePath = string.Empty;

    [ObservableProperty]
    private string _steelSeriesPathStatus = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _languageStatus = string.Empty;

    [ObservableProperty]
    private string _licenseStateText = string.Empty;

    [ObservableProperty]
    private string _licenseDetailText = string.Empty;

    public string EditionName =>
#if LICENSE_STORE_DEV
        "Store (Dev)";
#elif LICENSE_STORE
        "Microsoft Store";
#elif LICENSE_TRIAL_TEST
        $"Trial (Test {SonarController.Services.License.TrialTestSettings.TrialDays}d)";
#elif LICENSE_TRIAL
        "Trial";
#else
        "Full";
#endif

    [ObservableProperty]
    private bool _showPurchasePrompt;

    [ObservableProperty]
    private bool _isPurchaseUrgent;

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public IReadOnlyList<LanguageOption> LanguageOptions => _language.Languages;
    public DevicesViewModel Devices { get; }
    public ObservableCollection<DisplayOption> DisplayOptions { get; } = [];
    public InteractionModeService InteractionMode { get; }

    private readonly GlobalHotkeyService _hotkeyService;
    private readonly DisplayPreferenceService _displayPreferenceService;
    private readonly LanguageService _language;
    private readonly LicenseService _licenseService;
    private readonly IStartupService _startupService;
    private readonly SteelSeriesPathService _steelSeriesPathService;
    private string? _steelSeriesPathStatusKey;
    private string? _steelSeriesPathStatusDetail;

    public SettingsViewModel(
        DevicesViewModel devicesViewModel,
        GlobalHotkeyService hotkeyService,
        DisplayPreferenceService displayPreferenceService,
        InteractionModeService interactionMode,
        LanguageService language,
        LicenseService licenseService,
        IStartupService startupService,
        SteelSeriesPathService steelSeriesPathService)
    {
        _startupService = startupService;
        _steelSeriesPathService = steelSeriesPathService;
        _selectedSteelSeriesExecutablePath = steelSeriesPathService.ExecutablePath
            ?? SteelSeriesPathService.GetSuggestedExecutablePath();
        Devices = devicesViewModel;
        _hotkeyService = hotkeyService;
        _displayPreferenceService = displayPreferenceService;
        InteractionMode = interactionMode;
        _language = language;
        if (steelSeriesPathService.LoadError is not null)
            SetSteelSeriesPathStatus("SteelSeriesPathLoadError", steelSeriesPathService.LoadError);
        _licenseService = licenseService;
        _licenseService.StatusChanged += (_, status) => UpdateLicenseDisplay(status);
        _selectedLanguage = _language.Languages.First(option => option.Code == _language.CurrentLanguage);
        _languageStatus = _language.LoadError ?? string.Empty;
        _language.LanguageChanged += (_, _) =>
        {
            RefreshDisplayOptions();
            UpdateSteelSeriesPathStatus();
            DisplayStatus = string.Empty;
            if (IsCapturingHotkey)
                HotkeyStatus = _language.GetString("HotkeyCapture");
            else if (!string.IsNullOrEmpty(HotkeyStatus))
                HotkeyStatus = string.Empty;
            if (!string.IsNullOrEmpty(LanguageStatus))
                LanguageStatus = _language.GetString("LanguageSaved");
            UpdateLicenseDisplay(_licenseService.Current);
        };
        _hotkeyDisplay = hotkeyService.Current.DisplayName;
        _hotkeyStatus = hotkeyService.InitializationError ?? string.Empty;

        RefreshDisplayOptions();
        _displayStatus = displayPreferenceService.LoadError ?? string.Empty;

        _ = RefreshStartupAsync();
    }

    public async Task RefreshStartupAsync()
    {
        var enabled = await _startupService.IsEnabledAsync();
        _suppressEvents = true;
        IsAutoStartEnabled = enabled;
        _suppressEvents = false;
    }

    public async Task RefreshLicenseAsync(CancellationToken cancellationToken = default)
    {
        var status = await _licenseService.RefreshAsync(cancellationToken);
        UpdateLicenseDisplay(status);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private static void OpenPurchasePage() => PurchaseLink.Open();

    [ObservableProperty]
    private bool _showTrialInstalledNotice;

#if LICENSE_STORE || LICENSE_STORE_DEV
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void UninstallTrial()
    {
        var command = TrialUninstaller.FindUninstallCommand();
        if (command is not null)
            TrialUninstaller.TryStartUninstall(command);
    }
#else
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void UninstallTrial()
    {
    }
#endif

    private void UpdateLicenseDisplay(LicenseStatus status)
    {
#if LICENSE_STORE || LICENSE_STORE_DEV
        ShowTrialInstalledNotice = TrialUninstaller.FindUninstallCommand() is not null;
#endif
        ShowPurchasePrompt = status.ShouldPromptPurchase;
        IsPurchaseUrgent = status.IsPurchaseUrgent;
        LicenseStateText = _language.GetString(status.State switch
        {
            LicenseState.Trial => "LicenseTrial",
            LicenseState.Purchased => "LicensePurchased",
#if LICENSE_STORE_DEV
            LicenseState.Development => "LicenseDevelopment",
#endif
            LicenseState.Expired => "LicenseExpired",
            LicenseState.Unlicensed => "LicenseUnlicensed",
            _ => "LicenseUnavailable",
        });

        LicenseDetailText = status.State switch
        {
            LicenseState.Trial when status.ExpiresAt is not null =>
                string.Format(_language.GetString("LicenseTrialDetail"),
                    status.RemainingDays ?? 0,
                    status.ExpiresAt.Value.ToLocalTime().ToString("d")),
            LicenseState.Expired => _language.GetString("LicenseExpiredDetail"),
            LicenseState.Unavailable => _language.GetString("LicenseUnavailableDetail"),
            LicenseState.Unlicensed => _language.GetString("LicenseUnlicensedDetail"),
#if LICENSE_STORE_DEV
            LicenseState.Development => _language.GetString("LicenseDevelopmentDetail"),
#endif
            _ => _language.GetString("LicensePurchasedDetail"),
        };
    }

    public void RefreshDisplayOptions()
    {
        _suppressEvents = true;
        DisplayOptions.Clear();
        foreach (var option in _displayPreferenceService.GetDisplayOptions())
            DisplayOptions.Add(option);

        SelectedDisplay = FindSavedOption();
        _suppressEvents = false;
    }

    private DisplayOption? FindSavedOption() =>
        DisplayOptions.FirstOrDefault(option => option.MonitorId == _displayPreferenceService.PreferredId);

    partial void OnSelectedDisplayChanged(DisplayOption? value)
    {
        if (_suppressEvents || value is null) return;

        // 未接続の保存済みディスプレイは設定を保持したまま、そのまま選択状態にしておく
        if (!value.IsConnected) return;

        if (_displayPreferenceService.TrySetPreferredDisplay(value.MonitorId, out var error))
        {
            DisplayStatus = _language.GetString("DisplaySavedStatus");
            return;
        }

        DisplayStatus = error ?? _language.GetString("DisplayNotSaved");
        RefreshDisplayOptions();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null || value.Code == _language.CurrentLanguage) return;

        if (_language.TrySetLanguage(value.Code, out var error))
        {
            LanguageStatus = _language.GetString("LanguageSaved");
            return;
        }

        SelectedLanguage = _language.Languages.First(option => option.Code == _language.CurrentLanguage);
        LanguageStatus = error ?? _language.GetString("LanguageInvalid");
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = ApplyAutoStartAsync(value);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SaveSteelSeriesPath()
    {
        var result = _steelSeriesPathService.TrySetExecutablePath(
            SelectedSteelSeriesExecutablePath, out var error);
        switch (result)
        {
            case SteelSeriesPathSaveResult.Success:
                SelectedSteelSeriesExecutablePath = _steelSeriesPathService.ExecutablePath ?? string.Empty;
                SetSteelSeriesPathStatus("SteelSeriesPathSaved");
                break;
            case SteelSeriesPathSaveResult.InvalidPath:
                SetSteelSeriesPathStatus("SteelSeriesPathInvalid");
                break;
            case SteelSeriesPathSaveResult.SaveFailed:
                SetSteelSeriesPathStatus("SteelSeriesPathSaveError", error);
                break;
        }
    }

    private void SetSteelSeriesPathStatus(string? key, string? detail = null)
    {
        _steelSeriesPathStatusKey = key;
        _steelSeriesPathStatusDetail = detail;
        UpdateSteelSeriesPathStatus();
    }

    private void UpdateSteelSeriesPathStatus() =>
        SteelSeriesPathStatus = _steelSeriesPathStatusKey is null
            ? string.Empty
            : _steelSeriesPathStatusDetail is null
                ? _language.GetString(_steelSeriesPathStatusKey)
                : string.Format(_language.GetString(_steelSeriesPathStatusKey), _steelSeriesPathStatusDetail);

    private async Task ApplyAutoStartAsync(bool enabled)
    {
        var result = await _startupService.SetEnabledAsync(enabled);
        StartupStatus = result switch
        {
            StartupChangeResult.Success => string.Empty,
            StartupChangeResult.DisabledByUser => _language.GetString("StartupDisabledByUser"),
            StartupChangeResult.DisabledByPolicy => _language.GetString("StartupDisabledByPolicy"),
            _ => _language.GetString("StartupFailed"),
        };

        if (result != StartupChangeResult.Success)
            await RefreshStartupAsync();
    }

    public void BeginHotkeyCapture()
    {
        if (IsCapturingHotkey) return;

        _hotkeyService.Suspend();
        IsCapturingHotkey = true;
        HotkeyStatus = _language.GetString("HotkeyCapture");
    }

    public void CancelHotkeyCapture()
    {
        if (!IsCapturingHotkey) return;

        IsCapturingHotkey = false;
        _hotkeyService.Resume();
        HotkeyStatus = string.Empty;
    }

    public void CaptureHotkey(ModifierKeys modifiers, Key key)
    {
        if (key == Key.Escape)
        {
            CancelHotkeyCapture();
            return;
        }

        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (_hotkeyService.TrySetBinding(new HotkeyBinding(modifiers, key), out var error))
        {
            HotkeyDisplay = _hotkeyService.Current.DisplayName;
            IsCapturingHotkey = false;
            HotkeyStatus = _language.GetString("HotkeySaved");
            return;
        }

        HotkeyStatus = error ?? _language.GetString("HotkeySaveError");
    }
}
