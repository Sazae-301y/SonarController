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
    public async void ShowNearCursor()
    {
        if (_isOpening || IsVisible) return;

        _isOpening = true;
        var requestId = ++_showRequestId;
        Opacity = 0;

        try
        {
            // SizeToContent のため、一旦画面外に不透明度0で表示してから実サイズで位置決めする
            Show();
            UpdateLayout();
            RepositionNearCursor();

            await _viewModel.LoadAsync();

            // 読み込み中に外側クリックで非表示化されていた場合は再表示しない
            if (!IsVisible || requestId != _showRequestId) return;

            // チャンネル一覧の読み込みでサイズが変わるため、確定後に再度位置調整してから表示する
            UpdateLayout();
            RepositionNearCursor();
            Opacity = 1;
            Activate();

            _pollTimer.Start();
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
