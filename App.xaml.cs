using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using SonarController.Helpers;
using SonarController.Services;
using SonarController.Services.License;
using SonarController.ViewModels;
using SonarController.Views;
using Application = System.Windows.Application;

namespace SonarController;

public partial class App : Application
{
    // 多重起動防止用。GCされないようフィールドで保持する
    private static Mutex? _singleInstanceMutex;

    public IServiceProvider Services { get; private set; } = null!;

    private TaskbarIcon? _trayIcon;
    private System.IO.Stream? _trayIconStream;
    private TrayPopupWindow? _popupWindow;
    private SettingsWindow? _settingsWindow;
    private GlobalHotkeyService? _hotkeyService;
    private DispatcherTimer? _licenseRefreshTimer;

    // 期限切れはローカル時刻で即時判定するため、定期確認は粗くてよい。Storeは変更通知もある
#if LICENSE_STORE
    private static readonly TimeSpan LicenseRefreshInterval = TimeSpan.FromHours(4);
#else
    private static readonly TimeSpan LicenseRefreshInterval = TimeSpan.FromMinutes(30);
#endif

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLog.Initialize();
        AppLog.AttachToTrace();
        AppLog.Info($"Starting SonarController (edition: {GetMutexName()}, packaged: {PackageIdentity.IsPackaged})");
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled exception.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Warn("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };

        Services = ConfigureServices(() =>
            _popupWindow is null ? IntPtr.Zero : new WindowInteropHelper(_popupWindow).EnsureHandle());
        var language = Services.GetRequiredService<LanguageService>();

        _singleInstanceMutex = new Mutex(true, GetMutexName(), out var isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show(language.GetString("AlreadyRunning"), "SonarController",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _popupWindow = new TrayPopupWindow(
            Services.GetRequiredService<TrayViewModel>(),
            Services.GetRequiredService<DisplayPreferenceService>());
        _hotkeyService = Services.GetRequiredService<GlobalHotkeyService>();
        var hotkeyRegistered = _hotkeyService.Initialize(
            _popupWindow, () => _popupWindow.TogglePopup(ignoreRecentDeactivation: true), out var hotkeyError);
        _settingsWindow = new SettingsWindow(Services.GetRequiredService<SettingsViewModel>());

        SetupTrayIcon();

        if (!hotkeyRegistered)
        {
            AppLog.Warn($"Global hotkey registration failed: {hotkeyError}");
            _trayIcon?.ShowNotification(
                language.GetString("HotkeyConflictTitle"),
                hotkeyError ?? language.GetString("HotkeyInitError"),
                H.NotifyIcon.Core.NotificationIcon.Warning);
        }

        var licenseService = Services.GetRequiredService<LicenseService>();
        await licenseService.RefreshAsync();
        _licenseRefreshTimer = new DispatcherTimer { Interval = LicenseRefreshInterval };
        _licenseRefreshTimer.Tick += async (_, _) => await licenseService.RefreshAsync();
        _licenseRefreshTimer.Start();
    }

    private static string GetMutexName()
    {
#if LICENSE_STORE
        return "SonarController.Store.SingleInstance";
#elif LICENSE_TRIAL
        return "SonarController.Trial.SingleInstance";
#else
        return "SonarController.SingleInstance";
#endif
    }

    private static IServiceProvider ConfigureServices(Func<IntPtr> getStoreWindowHandle)
    {
        var services = new ServiceCollection();

        // API Layer
        services.AddSingleton<SonarAddressResolver>();
        services.AddSingleton<SonarHttpClient>();
        services.AddSingleton<ISonarService, SonarService>();
        services.AddSingleton<IAudioEndpointVolumeService, AudioEndpointVolumeService>();
        services.AddSingleton<DisplayPreferenceService>();
        services.AddSingleton<LanguageService>();
        services.AddSingleton<InteractionModeService>();

        // License（ビルド種別でプロバイダーを切り替える）
#if LICENSE_STORE
        services.AddSingleton<ILicenseProvider>(_ => new StoreLicenseProvider(getStoreWindowHandle, new TrialLicenseProvider()));
#elif LICENSE_TRIAL
        services.AddSingleton<ILicenseProvider, TrialLicenseProvider>();
#else
        services.AddSingleton<ILicenseProvider, FullLicenseProvider>();
#endif
        services.AddSingleton<LicenseService>();
        services.AddSingleton<IStartupService, StartupService>();

        // ViewModels（トレイポップアップ・設定ウィンドウとも常駐中は使い回すためSingleton）
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<GlobalHotkeyService>();

        return services.BuildServiceProvider();
    }

    private void SetupTrayIcon()
    {
        var iconResource = GetResourceStream(new Uri("pack://application:,,,/Assets/SonarController-Tray.ico"))
            ?? throw new InvalidOperationException("The application icon resource could not be loaded.");
        _trayIconStream = iconResource.Stream;

        _trayIcon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(_trayIconStream),
            ToolTipText = "SonarController",
        };
        _trayIcon.TrayLeftMouseUp += (_, _) => _popupWindow?.TogglePopup();

        var contextMenu = new System.Windows.Controls.ContextMenu();
        var openItem = new System.Windows.Controls.MenuItem();
        openItem.SetResourceReference(System.Windows.Controls.HeaderedContentControl.HeaderProperty, "Open");
        openItem.Click += (_, _) => _popupWindow?.ShowPopup();
        var settingsItem = new System.Windows.Controls.MenuItem();
        settingsItem.SetResourceReference(System.Windows.Controls.HeaderedContentControl.HeaderProperty, "SettingsTitle");
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        var exitItem = new System.Windows.Controls.MenuItem();
        exitItem.SetResourceReference(System.Windows.Controls.HeaderedContentControl.HeaderProperty, "Exit");
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
        _licenseRefreshTimer?.Stop();
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _trayIconStream?.Dispose();
        // 常駐アプリのため、通常のWindow.Close経由ではなくプロセスを直接終了する
        Environment.Exit(0);
    }
}
