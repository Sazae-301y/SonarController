using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SonarController2.Helpers;

namespace SonarController2.ViewModels;

/// <summary>
/// 設定ウィンドウ用 ViewModel。自動起動設定とバージョン情報を扱う。
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private bool _suppressEvents;

    [ObservableProperty]
    private bool _isAutoStartEnabled;

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public DevicesViewModel Devices { get; }

    public SettingsViewModel(DevicesViewModel devicesViewModel)
    {
        Devices = devicesViewModel;

        _suppressEvents = true;
        IsAutoStartEnabled = AutoStartHelper.IsEnabled();
        _suppressEvents = false;
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        if (_suppressEvents) return;
        AutoStartHelper.SetEnabled(value);
    }
}
