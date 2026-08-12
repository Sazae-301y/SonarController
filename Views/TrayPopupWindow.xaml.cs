using System.Windows;
using System.Windows.Threading;
using SonarController2.ViewModels;

namespace SonarController2.Views;

public partial class TrayPopupWindow : Window
{
    private readonly TrayViewModel _viewModel;
    private readonly DispatcherTimer _pollTimer;

    public TrayPopupWindow(TrayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Sonar側の内部連動（他チャンネルの音量変化等）を表示に反映するための定期ポーリング
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _pollTimer.Tick += async (_, _) => await _viewModel.RefreshVolumesAsync();
    }

    /// <summary>タスクトレイアイコン付近（カーソル位置基準）にポップアップを表示する</summary>
    public async void ShowNearCursor()
    {
        // SizeToContent のため、一旦画面外に不透明度0で表示してから実サイズで位置決めする
        Show();
        UpdateLayout();
        RepositionNearCursor();

        await _viewModel.LoadAsync();

        // チャンネル一覧の読み込みでサイズが変わるため、確定後に再度位置調整してから表示する
        UpdateLayout();
        RepositionNearCursor();
        Opacity = 1;
        Activate();

        _pollTimer.Start();
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
        _pollTimer.Stop();
        Hide();
    }

    public void TogglePopup()
    {
        if (IsVisible)
            HidePopup();
        else
            ShowNearCursor();
    }

    private void Window_Deactivated(object sender, EventArgs e) => HidePopup();

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HidePopup();
        ((App)System.Windows.Application.Current).ShowSettingsWindow();
    }
}
