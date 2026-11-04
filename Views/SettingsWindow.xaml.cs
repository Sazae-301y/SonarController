using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SonarController.ViewModels;

namespace SonarController.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly DispatcherTimer _scrollAnimationTimer;
    private double _scrollStart;
    private double _scrollTarget;
    private DateTime _scrollAnimationStarted;
    private static readonly TimeSpan ScrollAnimationDuration = TimeSpan.FromMilliseconds(180);

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _scrollAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _scrollAnimationTimer.Tick += ScrollAnimationTimer_Tick;
        // 常駐中にディスプレイが接続・切断されても、開くたびに一覧を最新化する
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) _viewModel.RefreshDisplayOptions();
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

    private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SettingsScrollViewer.ScrollableHeight <= 0) return;

        var currentTarget = _scrollAnimationTimer.IsEnabled
            ? _scrollTarget
            : SettingsScrollViewer.VerticalOffset;
        _scrollStart = SettingsScrollViewer.VerticalOffset;
        _scrollTarget = Math.Clamp(currentTarget - e.Delta / 120d * 112d,
            0, SettingsScrollViewer.ScrollableHeight);
        _scrollAnimationStarted = DateTime.UtcNow;
        e.Handled = true;

        if (!_scrollAnimationTimer.IsEnabled)
            _scrollAnimationTimer.Start();
    }

    private void ScrollAnimationTimer_Tick(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(
            (DateTime.UtcNow - _scrollAnimationStarted).TotalMilliseconds /
            ScrollAnimationDuration.TotalMilliseconds, 0, 1);
        var easedProgress = 1 - Math.Pow(1 - progress, 3);
        SettingsScrollViewer.ScrollToVerticalOffset(
            _scrollStart + (_scrollTarget - _scrollStart) * easedProgress);

        if (progress < 1) return;

        SettingsScrollViewer.ScrollToVerticalOffset(_scrollTarget);
        _scrollAnimationTimer.Stop();
    }

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
        _scrollAnimationTimer.Stop();
        _viewModel.CancelHotkeyCapture();
        base.OnClosing(e);
        Hide();
        e.Cancel = true;
    }
}
