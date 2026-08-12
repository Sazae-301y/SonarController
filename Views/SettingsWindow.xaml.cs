using System.ComponentModel;
using System.Windows;
using SonarController2.ViewModels;

namespace SonarController2.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.Devices.LoadAsync();
    }

    // 常駐アプリのため、設定ウィンドウは閉じるだけでプロセスは終了させない
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        Hide();
        e.Cancel = true;
    }
}
