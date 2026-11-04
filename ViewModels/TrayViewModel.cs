using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SonarController.Helpers;
using SonarController.Models;
using SonarController.Services;
using SonarController.Services.License;
using System.Collections.ObjectModel;

namespace SonarController.ViewModels;

/// <summary>
/// トレイのコンパクトポップアップ用 ViewModel。
/// 旧 MixerViewModel + StreamerViewModel の機能を1画面に統合したもの。
/// </summary>
public partial class TrayViewModel : ViewModelBase
{
    private static readonly SonarChannel[] AllChannels =
    {
        SonarChannel.Master,
        SonarChannel.Game,
        SonarChannel.Chat,
        SonarChannel.Media,
        SonarChannel.Aux,
        SonarChannel.Mic,
    };

    private readonly ISonarService _sonar;
    private readonly LanguageService _language;
    private readonly LicenseService _licenseService;
    private bool _suppressEvents;
    private bool _needsReload;

    public InteractionModeService InteractionMode { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLicenseUsable;

    [ObservableProperty]
    private bool _isLicenseBlocked;

    [ObservableProperty]
    private string _licenseNoticeText = string.Empty;

    [ObservableProperty]
    private bool _isStreamerMode;

    // ---- Streamer Mode: Audience Monitoring ----
    [ObservableProperty]
    private bool _audienceMonitoring;

    public ObservableCollection<ChannelViewModel> Channels { get; } = new();
    public ObservableCollection<DeviceMixViewModel> DeviceMixes { get; } = new();

    public TrayViewModel(
        ISonarService sonar,
        IAudioEndpointVolumeService endpointVolume,
        InteractionModeService interactionMode,
        LanguageService language,
        LicenseService licenseService)
    {
        _sonar = sonar;
        _language = language;
        _licenseService = licenseService;
        InteractionMode = interactionMode;
        UpdateLicenseStatus(_licenseService.Current);
        _licenseService.StatusChanged += (_, status) => UpdateLicenseStatus(status);
        _language.LanguageChanged += (_, _) => UpdateLicenseStatus(_licenseService.Current);

        foreach (var channel in AllChannels)
            Channels.Add(new ChannelViewModel(_sonar, channel, language));

        DeviceMixes.Add(new DeviceMixViewModel(sonar, endpointVolume, "PersonalMix", SonarMix.Personal, isInput: false, language: language));
        DeviceMixes.Add(new DeviceMixViewModel(sonar, endpointVolume, "StreamMix", SonarMix.Stream, isInput: false, language: language));
        DeviceMixes.Add(new DeviceMixViewModel(sonar, endpointVolume, "MicrophoneInput", mix: null, isInput: true, language: language));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        _needsReload = false;

        try
        {
            if (!_licenseService.IsUsable)
            {
                UpdateLicenseStatus(_licenseService.Current);
                return;
            }

            var mode = await _sonar.GetModeAsync();

            _suppressEvents = true;
            IsStreamerMode = mode == SonarMode.Streamer;
            AudienceMonitoring = await _sonar.GetAudienceMonitoringAsync();
            _suppressEvents = false;

            var devices = await _sonar.GetAudioDevicesAsync();
            foreach (var deviceMix in DeviceMixes)
                await deviceMix.LoadAsync(devices);

            await LoadChannelsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = SonarErrorMessages.Describe(ex, _language);
            _needsReload = true;
            AppLog.Warn($"Failed to load Sonar state: {ex.GetType().Name}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadChannelsAsync()
    {
        var routing = IsStreamerMode
            ? await _sonar.GetRoutingStatusesAsync()
            : null;

        foreach (var vm in Channels)
        {
            var eqConfigs = vm.HasEq
                ? await _sonar.GetFavoriteEqConfigsAsync(vm.Channel)
                : Array.Empty<EqConfig>();
            var selectedEq = vm.HasEq
                ? await _sonar.GetSelectedEqConfigAsync(vm.Channel)
                : null;

            if (IsStreamerMode)
            {
                var personal = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Personal);
                var stream = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Stream);
                var personalRouted = !vm.HasRouting || (routing!.TryGetValue((vm.Channel, SonarMix.Personal), out var pr) && pr);
                var streamRouted = !vm.HasRouting || (routing!.TryGetValue((vm.Channel, SonarMix.Stream), out var sr) && sr);
                vm.LoadStreamerState(personal, stream, personalRouted, streamRouted, eqConfigs, selectedEq);
            }
            else
            {
                var vol = await _sonar.GetVolumeAsync(vm.Channel);
                vm.LoadClassicState(vol, eqConfigs, selectedEq);
            }
        }
    }

    /// <summary>
    /// Sonar側の音量をポーリングして反映する（EQ再取得はしない軽量版）。
    /// 別チャンネル/別ミックスの値がSonar側の内部連動で変わった場合に追従させるために使う。
    /// </summary>
    public async Task RefreshVolumesAsync()
    {
        if (!_licenseService.IsUsable) return;

        // 接続エラー後（GGの再起動など）は、Sonarに再接続できた時点で全体を再読込して自動復旧する
        if (_needsReload)
        {
            if (!IsLoading && await _sonar.IsSonarAvailableAsync()) await LoadAsync();
            return;
        }

        try
        {
            foreach (var vm in Channels)
            {
                if (IsStreamerMode)
                {
                    var personal = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Personal);
                    var stream = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Stream);
                    vm.RefreshStreamerVolumes(personal, stream);
                }
                else
                {
                    var vol = await _sonar.GetVolumeAsync(vm.Channel);
                    vm.RefreshClassicVolume(vol);
                }
            }
        }
        catch (Exception ex) when (ex is SonarNotAvailableException or SonarApiException)
        {
            ErrorMessage = SonarErrorMessages.Describe(ex, _language);
            _needsReload = true;
            AppLog.Warn($"Lost connection to Sonar while polling: {ex.GetType().Name}");
        }
        catch
        {
            // ポーリング中のその他のエラーはUIに表示せず次回に持ち越す
        }
    }

    partial void OnIsStreamerModeChanged(bool value)
    {
        if (_suppressEvents || !_licenseService.IsUsable) return;
        _ = ApplyModeAsync(value);
    }

    partial void OnAudienceMonitoringChanged(bool value)
    {
        if (_suppressEvents || !_licenseService.IsUsable) return;
        _ = _sonar.SetAudienceMonitoringAsync(value);
    }

    private void UpdateLicenseStatus(LicenseStatus status)
    {
        IsLicenseUsable = _licenseService.IsUsable;
        IsLicenseBlocked = !IsLicenseUsable;
        LicenseNoticeText = status.State switch
        {
            LicenseState.Expired => _language.GetString("LicenseExpiredDetail"),
            LicenseState.Unavailable => _language.GetString("LicenseUnavailableDetail"),
            _ => string.Empty,
        };
    }

    private async Task ApplyModeAsync(bool streamer)
    {
        try
        {
            await _sonar.SetModeAsync(streamer ? SonarMode.Streamer : SonarMode.Classic);
            // モードが切り替わると各チャンネルの参照先(Classic/Streamer)が変わるため再読込する
            await LoadChannelsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = string.Format(_language.GetString("ModeChangeError"), ex.Message);
        }
    }
}
