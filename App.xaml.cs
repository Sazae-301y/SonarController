using System.Threading;
using System.Windows;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using SonarController2.Services;
using SonarController2.ViewModels;
using SonarController2.Views;
using Application = System.Windows.Application;

namespace SonarController2;

public partial class App : Application
{
    // 多重起動防止用。GCされないようフィールドで保持する
    private static Mutex? _singleInstanceMutex;

    public IServiceProvider Services { get; private set; } = null!;

    private TaskbarIcon? _trayIcon;
    private TrayPopupWindow? _popupWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "SonarController2.SingleInstance", out var isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show("SonarController2は既に起動しています。", "SonarController2",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Services = ConfigureServices();

        _popupWindow = new TrayPopupWindow(Services.GetRequiredService<TrayViewModel>());
        _settingsWindow = new SettingsWindow(Services.GetRequiredService<SettingsViewModel>());

        SetupTrayIcon();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // API Layer
        services.AddSingleton<SonarAddressResolver>();
        services.AddSingleton<SonarHttpClient>();
        services.AddSingleton<ISonarService, SonarService>();
        services.AddSingleton<IAudioEndpointVolumeService, AudioEndpointVolumeService>();

        // ViewModels（トレイポップアップ・設定ウィンドウとも常駐中は使い回すためSingleton）
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            ToolTipText = "SonarController2",
        };
        _trayIcon.TrayLeftMouseUp += (_, _) => _popupWindow?.TogglePopup();

        var contextMenu = new System.Windows.Controls.ContextMenu();
        var openItem = new System.Windows.Controls.MenuItem { Header = "開く" };
        openItem.Click += (_, _) => _popupWindow?.ShowNearCursor();
        var settingsItem = new System.Windows.Controls.MenuItem { Header = "設定" };
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        var exitItem = new System.Windows.Controls.MenuItem { Header = "終了" };
        exitItem.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(exitItem);
        _trayIcon.ContextMenu = contextMenu;

        _trayIcon.ForceCreate();
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null) return;
        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        // 常駐アプリのため、通常のWindow.Close経由ではなくプロセスを直接終了する
        Environment.Exit(0);
    }
}
