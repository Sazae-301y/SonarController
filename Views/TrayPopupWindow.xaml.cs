using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SonarController.Helpers;
using SonarController.ViewModels;

namespace SonarController.Views;

public partial class TrayPopupWindow : Window
{
    private static readonly TimeSpan ReopenSuppressWindow = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan DropdownScrollDuration = TimeSpan.FromMilliseconds(110);

    private readonly TrayViewModel _viewModel;
    private readonly DisplayPreferenceService _displayPreferenceService;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _dropdownScrollTimer;
    private DateTime _lastDeactivatedAt = DateTime.MinValue;
    private DateTime _dropdownScrollStartedAt;
    private ScrollViewer? _dropdownScrollViewer;
    private TranslateTransform? _dropdownScrollTransform;
    private double _dropdownScrollStartOffset;
    private double _dropdownScrollTargetOffset;
    private bool _isOpening;
    private bool _isRefreshing;
    private int _showRequestId;
    private Slider? _lastTouchedSlider;

    public TrayPopupWindow(TrayViewModel viewModel, DisplayPreferenceService displayPreferenceService)
    {
        InitializeComponent();
        // Slider標準のクリック移動処理より先に受けるため、ウィンドウ側のトンネルイベントで処理する
        AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Window_PreviewMouseLeftButtonDown), true);
        PreviewKeyDown += Window_PreviewKeyDown;
        _viewModel = viewModel;
        _displayPreferenceService = displayPreferenceService;
        DataContext = _viewModel;

        _dropdownScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dropdownScrollTimer.Tick += DropdownScrollTimer_Tick;

        // Sonar側の内部連動（他チャンネルの音量変化等）を表示に反映するための定期ポーリング
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _pollTimer.Tick += async (_, _) =>
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                await _viewModel.RefreshVolumesAsync();
            }
            finally
            {
                _isRefreshing = false;
            }
        };
    }

    /// <summary>タスクトレイアイコン付近（カーソル位置基準）にポップアップを表示する</summary>
    public void ShowPopup()
    {
        if (_isOpening || IsVisible) return;

        _isOpening = true;
        var requestId = ++_showRequestId;

        // 既存のチャンネル枠を先に表示し、データ取得は表示後に行う
        Opacity = 1;
        Show();
        UpdateLayout();
        RepositionBottomRight();
        Activate();
        _pollTimer.Start();

        _ = LoadAfterShownAsync(requestId);
    }

    private async Task LoadAfterShownAsync(int requestId)
    {
        try
        {
            await _viewModel.LoadAsync();

            if (!IsVisible || requestId != _showRequestId) return;

            UpdateLayout();
            RepositionBottomRight();
        }
        finally
        {
            _isOpening = false;
        }
    }

    private async void DeviceComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox { DataContext: DeviceMixViewModel deviceMix })
            await _viewModel.RefreshDevicesAsync(deviceMix);
    }

    private void DropdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer
            || scrollViewer.ScrollableHeight <= 0
            || scrollViewer.Content is not UIElement content)
            return;

        // タッチパッドは細かい delta が連続して届くため、アニメーションせず即時に追従させる
        if (Math.Abs(e.Delta) % 120 != 0)
        {
            CompleteDropdownScroll();
            scrollViewer.ScrollToVerticalOffset(Math.Clamp(
                scrollViewer.VerticalOffset - e.Delta / 120.0 * 48.0,
                0.0,
                scrollViewer.ScrollableHeight));
            e.Handled = true;
            return;
        }

        var transform = GetDropdownScrollTransform(content);
        var currentOffset = ReferenceEquals(_dropdownScrollViewer, scrollViewer)
            && _dropdownScrollTimer.IsEnabled
                ? scrollViewer.VerticalOffset - transform.Y
                : scrollViewer.VerticalOffset;
        var targetOffset = Math.Clamp(
            currentOffset - e.Delta / 120.0 * 48.0,
            0.0,
            scrollViewer.ScrollableHeight);
        if (Math.Abs(targetOffset - currentOffset) < 0.1)
            return;

        _dropdownScrollViewer = scrollViewer;
        _dropdownScrollTransform = transform;
        _dropdownScrollStartOffset = currentOffset;
        _dropdownScrollTargetOffset = targetOffset;
        _dropdownScrollStartedAt = DateTime.UtcNow;
        _dropdownScrollTimer.Start();
        e.Handled = true;
    }

    private static TranslateTransform GetDropdownScrollTransform(UIElement content)
    {
        if (content.RenderTransform is TranslateTransform transform)
            return transform;

        transform = new TranslateTransform();
        content.RenderTransform = transform;
        return transform;
    }

    private void DropdownScrollTimer_Tick(object? sender, EventArgs e)
    {
        if (_dropdownScrollViewer is null || _dropdownScrollTransform is null)
        {
            _dropdownScrollTimer.Stop();
            return;
        }

        var progress = Math.Clamp(
            (DateTime.UtcNow - _dropdownScrollStartedAt).TotalMilliseconds / DropdownScrollDuration.TotalMilliseconds,
            0.0,
            1.0);
        if (progress >= 1.0)
        {
            CompleteDropdownScroll();
            return;
        }

        var easedProgress = 1.0 - Math.Pow(1.0 - progress, 2.0);
        var animatedOffset = _dropdownScrollStartOffset
            + (_dropdownScrollTargetOffset - _dropdownScrollStartOffset) * easedProgress;
        _dropdownScrollTransform.Y = _dropdownScrollViewer.VerticalOffset - animatedOffset;
    }

    // 進行中のアニメーションを最終位置で確定し、タッチ・キー操作など他のスクロール要因との競合を避ける
    private void CompleteDropdownScroll()
    {
        _dropdownScrollTimer.Stop();
        if (_dropdownScrollViewer is not null && _dropdownScrollTransform is not null)
        {
            _dropdownScrollViewer.ScrollToVerticalOffset(_dropdownScrollTargetOffset);
            _dropdownScrollViewer.UpdateLayout();
            _dropdownScrollTransform.Y = 0.0;
        }

        _dropdownScrollViewer = null;
        _dropdownScrollTransform = null;
    }

    private void DropdownScrollViewer_PreviewTouchDown(object sender, TouchEventArgs e) => CompleteDropdownScroll();

    private void RepositionBottomRight()
    {
        // 別DPIのモニターへ移るとサイズが変わるため、DPIが変わった場合はもう一度配置し直す
        var dpiBefore = VisualTreeHelper.GetDpi(this).DpiScaleX;
        RepositionOnce();
        UpdateLayout();
        if (Math.Abs(VisualTreeHelper.GetDpi(this).DpiScaleX - dpiBefore) > 0.001)
            RepositionOnce();
    }

    private void RepositionOnce()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cursor = ScreenHelper.GetCursorPosition();
        var screen = _displayPreferenceService.GetDisplayForPopup(cursor);
        var workArea = screen.WorkingArea;
        // 物理ピクセルで計算するため、余白はポップアップが載っているモニターのDPIで換算する
        var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var taskbarReserve = workArea.Bottom == screen.Bounds.Bottom ? (int)Math.Round(48 * dpiScale) : 0;
        var margin = (int)Math.Round(8 * dpiScale);

        var handle = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(handle, out var windowRect))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var width = windowRect.Right - windowRect.Left;
        var height = windowRect.Bottom - windowRect.Top;
        var left = Math.Max(workArea.Right - width - margin, workArea.Left + margin);
        var top = Math.Max(
            workArea.Bottom - height - taskbarReserve - margin,
            workArea.Top + margin);

        if (!SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void HidePopup()
    {
        _showRequestId++;
        _pollTimer.Stop();
        CompleteDropdownScroll();
        Hide();
    }

    public void TogglePopup(bool ignoreRecentDeactivation = false)
    {
        if (IsVisible)
        {
            HidePopup();
            return;
        }

        // トレイアイコンクリック直後の非アクティブ化で閉じたばかりの場合、同じクリックで再度開かないようにする
        if (!ignoreRecentDeactivation && (DateTime.UtcNow - _lastDeactivatedAt) < ReopenSuppressWindow)
            return;

        ShowPopup();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (PinToggle.IsChecked == true) return;

        _lastDeactivatedAt = DateTime.UtcNow;
        HidePopup();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HidePopup();
        ((App)System.Windows.Application.Current).ShowSettingsWindow();
    }

    // つまみ以外を押した場合は、その位置へ値を移したうえでThumbのドラッグ操作を開始し、
    // ボタンを離すまでそのまま調整できるようにする。
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Slider>(e.OriginalSource as DependencyObject) is not { } slider)
            return;

        _lastTouchedSlider = slider;

        if (slider.Template.FindName("PART_Track", slider) is not Track { Thumb: { } thumb } track
            || thumb.IsMouseOver
            || thumb.IsDragging)
        {
            return;
        }

        slider.SetCurrentValue(Slider.ValueProperty, track.ValueFromPoint(e.GetPosition(track)));
        // つまみの位置を更新してからドラッグを始めないと、基準点がずれて値が大きく動く
        slider.UpdateLayout();
        thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = thumb
        });
        e.Handled = true;
    }

    // 最後に触ったスライダーを矢印キーで1%ずつ調整する（デバイス選択のコンボボックス操作中は除く）
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        CompleteDropdownScroll();

        var step = e.Key switch
        {
            Key.Left or Key.Down => -0.01,
            Key.Right or Key.Up => 0.01,
            _ => 0.0
        };

        if (step == 0.0
            || _lastTouchedSlider is not { IsVisible: true, IsEnabled: true } slider
            || FindAncestor<System.Windows.Controls.ComboBox>(Keyboard.FocusedElement as DependencyObject) is not null)
        {
            return;
        }

        slider.SetCurrentValue(Slider.ValueProperty, Math.Round(slider.Value + step, 2));
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match) return match;
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
