using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SonarController2.Models;
using SonarController2.Services;
using System.Collections.ObjectModel;

namespace SonarController2.ViewModels;

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
    private bool _suppressEvents;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isStreamerMode;

    // ---- Streamer Mode: Audience Monitoring ----
    [ObservableProperty]
    private bool _audienceMonitoring;

    public ObservableCollection<ChannelViewModel> Channels { get; } = new();

    public TrayViewModel(ISonarService sonar)
    {
        _sonar = sonar;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var mode = await _sonar.GetModeAsync();

            _suppressEvents = true;
            IsStreamerMode = mode == SonarMode.Streamer;
            AudienceMonitoring = await _sonar.GetAudienceMonitoringAsync();
            _suppressEvents = false;

            await LoadChannelsAsync();
        }
        catch (SonarNotAvailableException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"エラーが発生しました: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadChannelsAsync()
    {
        if (Channels.Count == 0)
        {
            foreach (var ch in AllChannels)
                Channels.Add(new ChannelViewModel(_sonar, ch));
        }

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
        try
        {
            var routing = IsStreamerMode
                ? await _sonar.GetRoutingStatusesAsync()
                : null;

            foreach (var vm in Channels)
            {
                if (IsStreamerMode)
                {
                    var personal = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Personal);
                    var stream = await _sonar.GetVolumeAsync(vm.Channel, SonarMix.Stream);
                    var personalRouted = !vm.HasRouting || (routing!.TryGetValue((vm.Channel, SonarMix.Personal), out var pr) && pr);
                    var streamRouted = !vm.HasRouting || (routing!.TryGetValue((vm.Channel, SonarMix.Stream), out var sr) && sr);
                    vm.RefreshStreamerVolumes(personal, stream, personalRouted, streamRouted);
                }
                else
                {
                    var vol = await _sonar.GetVolumeAsync(vm.Channel);
                    vm.RefreshClassicVolume(vol);
                }
            }
        }
        catch
        {
            // ポーリング中のエラーはUIに表示せず次回に持ち越す
        }
    }

    partial void OnIsStreamerModeChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = ApplyModeAsync(value);
    }

    partial void OnAudienceMonitoringChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = _sonar.SetAudienceMonitoringAsync(value);
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
            ErrorMessage = $"モード切替失敗: {ex.Message}";
        }
    }
}
