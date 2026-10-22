using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SonarController2.Helpers;
using System.Windows.Input;

namespace SonarController2.ViewModels;

/// <summary>
/// 設定ウィンドウ用 ViewModel。自動起動設定とバージョン情報を扱う。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private bool _suppressEvents;

    [ObservableProperty]
    private bool _isAutoStartEnabled;

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

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public DevicesViewModel Devices { get; }
    public ObservableCollection<DisplayOption> DisplayOptions { get; } = [];

    private readonly GlobalHotkeyService _hotkeyService;
    private readonly DisplayPreferenceService _displayPreferenceService;

    public SettingsViewModel(
        DevicesViewModel devicesViewModel,
        GlobalHotkeyService hotkeyService,
        DisplayPreferenceService displayPreferenceService)
    {
        Devices = devicesViewModel;
        _hotkeyService = hotkeyService;
        _displayPreferenceService = displayPreferenceService;
        _hotkeyDisplay = hotkeyService.Current.DisplayName;
        _hotkeyStatus = hotkeyService.InitializationError ?? string.Empty;

        RefreshDisplayOptions();
        _displayStatus = displayPreferenceService.LoadError ?? string.Empty;

        _suppressEvents = true;
        IsAutoStartEnabled = AutoStartHelper.IsEnabled();
        _suppressEvents = false;
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
            DisplayStatus = "表示先を保存しました。";
            return;
        }

        DisplayStatus = error ?? "表示先を保存できませんでした。";
        RefreshDisplayOptions();
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        if (_suppressEvents) return;
        AutoStartHelper.SetEnabled(value);
    }

    public void BeginHotkeyCapture()
    {
        if (IsCapturingHotkey) return;

        _hotkeyService.Suspend();
        IsCapturingHotkey = true;
        HotkeyStatus = "キーまたはキーの組み合わせを押してください（Escでキャンセル）";
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
            HotkeyStatus = "ホットキーを保存しました。";
            return;
        }

        HotkeyStatus = error ?? "ホットキーを設定できませんでした。";
    }
}
