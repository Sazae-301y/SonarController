using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SonarController.ViewModels;

namespace SonarController.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        // 常駐中にディスプレイが接続・切断されても、開くたびに一覧を最新化する
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                _viewModel.RefreshDisplayOptions();
                _ = _viewModel.RefreshLicenseAsync();
                _ = _viewModel.RefreshStartupAsync();
            }
        };
        Loaded += async (_, _) => await _viewModel.Devices.LoadAsync();
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.BeginHotkeyCapture();
        Activate();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !e.Handled && Mouse.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SettingsScrollViewer_ManipulationBoundaryFeedback(
        object sender, ManipulationBoundaryFeedbackEventArgs e) =>
        e.Handled = true;

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_viewModel.IsCapturingHotkey) return;

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        _viewModel.CaptureHotkey(Keyboard.Modifiers, key);
    }

    // 常駐アプリのため、設定ウィンドウは閉じるだけでプロセスは終了させない
    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.CancelHotkeyCapture();
        base.OnClosing(e);
        Hide();
        e.Cancel = true;
    }
}
