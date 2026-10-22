using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SonarController2.Helpers;
using SonarController2.ViewModels;

namespace SonarController2.Views;

public partial class TrayPopupWindow : Window
{
    private static readonly TimeSpan ReopenSuppressWindow = TimeSpan.FromMilliseconds(200);

    private readonly TrayViewModel _viewModel;
    private readonly DisplayPreferenceService _displayPreferenceService;
    private readonly DispatcherTimer _pollTimer;
    private DateTime _lastDeactivatedAt = DateTime.MinValue;
    private bool _isOpening;
    private bool _isRefreshing;
    private int _showRequestId;

    public TrayPopupWindow(TrayViewModel viewModel, DisplayPreferenceService displayPreferenceService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _displayPreferenceService = displayPreferenceService;
        DataContext = _viewModel;

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

    private void RepositionBottomRight()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = _displayPreferenceService.GetDisplayForPopup(cursor);
        var workArea = screen.WorkingArea;
        var taskbarReserve = workArea.Bottom == screen.Bounds.Bottom ? 48 : 0;
        const int margin = 8;

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
