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

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public DevicesViewModel Devices { get; }

    private readonly GlobalHotkeyService _hotkeyService;

    public SettingsViewModel(DevicesViewModel devicesViewModel, GlobalHotkeyService hotkeyService)
    {
        Devices = devicesViewModel;
        _hotkeyService = hotkeyService;
        _hotkeyDisplay = hotkeyService.Current.DisplayName;
        _hotkeyStatus = hotkeyService.InitializationError ?? string.Empty;

        _suppressEvents = true;
        IsAutoStartEnabled = AutoStartHelper.IsEnabled();
        _suppressEvents = false;
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
