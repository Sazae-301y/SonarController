using System.Windows;
using System.Windows.Threading;
using SonarController2.ViewModels;

namespace SonarController2.Views;

public partial class TrayPopupWindow : Window
{
    private static readonly TimeSpan ReopenSuppressWindow = TimeSpan.FromMilliseconds(200);

    private readonly TrayViewModel _viewModel;
    private readonly DispatcherTimer _pollTimer;
    private DateTime _lastDeactivatedAt = DateTime.MinValue;
    private bool _isOpening;
    private bool _isRefreshing;
    private int _showRequestId;

    public TrayPopupWindow(TrayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
    public void ShowNearCursor()
    {
        if (_isOpening || IsVisible) return;

        _isOpening = true;
        var requestId = ++_showRequestId;

        // 既存のチャンネル枠を先に表示し、データ取得は表示後に行う
        Opacity = 1;
        Show();
        UpdateLayout();
        RepositionNearCursor();
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
            RepositionNearCursor();
        }
        finally
        {
            _isOpening = false;
        }
    }

    private void RepositionNearCursor()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cursor = System.Windows.Forms.Cursor.Position;
        var workArea = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;

        Left = Math.Min(cursor.X, workArea.Right - ActualWidth);
        Top = Math.Min(cursor.Y, workArea.Bottom - ActualHeight);
        Left = Math.Max(Left, workArea.Left);
        Top = Math.Max(Top, workArea.Top);
    }

    public void HidePopup()
    {
        _showRequestId++;
        _pollTimer.Stop();
        Hide();
    }

    public void TogglePopup()
    {
        if (IsVisible)
        {
            HidePopup();
            return;
        }

        // トレイアイコンクリック直後の非アクティブ化で閉じたばかりの場合、同じクリックで再度開かないようにする
        if ((DateTime.UtcNow - _lastDeactivatedAt) < ReopenSuppressWindow)
            return;

        ShowNearCursor();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        _lastDeactivatedAt = DateTime.UtcNow;
        HidePopup();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HidePopup();
        ((App)System.Windows.Application.Current).ShowSettingsWindow();
    }
}
