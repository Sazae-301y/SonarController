using CommunityToolkit.Mvvm.ComponentModel;
using SonarController2.Models;
using SonarController2.Services;
using System.Collections.ObjectModel;

namespace SonarController2.ViewModels;

/// <summary>
/// 各チャンネル（Master, Game, Chat, ...）の音量・ミュート・EQプリセットを保持する ViewModel。
/// Classic モードでは単一音量、Streamer モードでは Personal/Stream の2ミックス分の音量を扱う。
/// </summary>
public partial class ChannelViewModel : ViewModelBase
{
    private readonly ISonarService _sonar;
    private bool _suppressEvents;

    // 直近にユーザーがスライダーを操作した時刻。ポーリング更新でその値を一時的に上書きしないためのガード
    private static readonly TimeSpan LocalChangeGracePeriod = TimeSpan.FromMilliseconds(800);
    private DateTime _classicVolumeChangedAt = DateTime.MinValue;
    private DateTime _personalVolumeChangedAt = DateTime.MinValue;
    private DateTime _streamVolumeChangedAt = DateTime.MinValue;

    public SonarChannel Channel { get; }
    public string DisplayName => Channel.ToDisplayName();
    public bool HasEq => Channel != SonarChannel.Master;
    public bool HasRouting => Channel != SonarChannel.Master;

    /// <summary>チャンネルごとのアクセントカラー（UI装飾用）</summary>
    public string AccentColor => Channel switch
    {
        SonarChannel.Master => "#B0BEC5",
        SonarChannel.Game   => "#7C4DFF",
        SonarChannel.Chat   => "#29B6F6",
        SonarChannel.Media  => "#EC407A",
        SonarChannel.Aux    => "#7E57C2",
        SonarChannel.Mic    => "#FFA726",
        _ => "#FFFFFF"
    };

    /// <summary>チャンネルごとのアイコン絵文字（UI装飾用）</summary>
    public string Icon => Channel switch
    {
        SonarChannel.Master => "🎚️",
        SonarChannel.Game   => "🎮",
        SonarChannel.Chat   => "💬",
        SonarChannel.Media  => "📺",
        SonarChannel.Aux    => "🎧",
        SonarChannel.Mic    => "🎤",
        _ => "🔊"
    };

    [ObservableProperty]
    private bool _isStreamerMode;

    // ---- Classic mode ----
    [ObservableProperty]
    private double _classicVolume;

    [ObservableProperty]
    private bool _classicMuted;

    // ---- Streamer mode: Personal Mix ----
    [ObservableProperty]
    private double _personalVolume;

    [ObservableProperty]
    private bool _personalMuted;

    [ObservableProperty]
    private bool _personalRouted;

    // ---- Streamer mode: Stream Mix ----
    [ObservableProperty]
    private double _streamVolume;

    [ObservableProperty]
    private bool _streamMuted;

    [ObservableProperty]
    private bool _streamRouted;

    [ObservableProperty]
    private EqConfig? _selectedEqConfig;

    public ObservableCollection<EqConfig> EqConfigs { get; } = new();

    public ChannelViewModel(ISonarService sonar, SonarChannel channel)
    {
        _sonar = sonar;
        Channel = channel;
    }

    partial void OnClassicVolumeChanged(double value)
    {
        if (_suppressEvents) return;
        _classicVolumeChangedAt = DateTime.UtcNow;
        _ = TryAsync(() => _sonar.SetVolumeAsync(Channel, Math.Clamp(value, 0.0, 1.0)));
    }

    partial void OnClassicMutedChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = TryAsync(() => _sonar.SetMuteAsync(Channel, value));
    }

    partial void OnPersonalVolumeChanged(double value)
    {
        if (_suppressEvents) return;
        _personalVolumeChangedAt = DateTime.UtcNow;
        _ = TryAsync(() => _sonar.SetVolumeAsync(Channel, SonarMix.Personal, Math.Clamp(value, 0.0, 1.0)));
    }

    partial void OnPersonalMutedChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = TryAsync(() => _sonar.SetMuteAsync(Channel, SonarMix.Personal, value));
    }

    partial void OnStreamVolumeChanged(double value)
    {
        if (_suppressEvents) return;
        _streamVolumeChangedAt = DateTime.UtcNow;
        _ = TryAsync(() => _sonar.SetVolumeAsync(Channel, SonarMix.Stream, Math.Clamp(value, 0.0, 1.0)));
    }

    partial void OnStreamMutedChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = TryAsync(() => _sonar.SetMuteAsync(Channel, SonarMix.Stream, value));
    }

    partial void OnPersonalRoutedChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = TryAsync(() => _sonar.SetRoutingAsync(Channel, SonarMix.Personal, value));
    }

    partial void OnStreamRoutedChanged(bool value)
    {
        if (_suppressEvents) return;
        _ = TryAsync(() => _sonar.SetRoutingAsync(Channel, SonarMix.Stream, value));
    }

    partial void OnSelectedEqConfigChanged(EqConfig? value)
    {
        if (_suppressEvents || value is null) return;
        _ = TryAsync(() => _sonar.SetEqConfigAsync(value.Id));
    }

    private static async Task TryAsync(Func<Task> action)
    {
        try { await action(); }
        catch { /* 失敗しても UI は動き続ける */ }
    }

    /// <summary>Classic モードの値をセットする（変更イベントを抑止する）</summary>
    public void LoadClassicState(VolumeSettings vol, IReadOnlyList<EqConfig> eqConfigs, EqConfig? selectedEq)
    {
        _suppressEvents = true;
        IsStreamerMode = false;
        ClassicVolume = vol.Volume;
        ClassicMuted = vol.Muted;
        LoadEqState(eqConfigs, selectedEq);
        _suppressEvents = false;
    }

    /// <summary>Streamer モードの値をセットする（変更イベントを抑止する）</summary>
    public void LoadStreamerState(
        VolumeSettings personal, VolumeSettings stream,
        bool personalRouted, bool streamRouted,
        IReadOnlyList<EqConfig> eqConfigs, EqConfig? selectedEq)
    {
        _suppressEvents = true;
        IsStreamerMode = true;
        PersonalVolume = personal.Volume;
        PersonalMuted = personal.Muted;
        PersonalRouted = personalRouted;
        StreamVolume = stream.Volume;
        StreamMuted = stream.Muted;
        StreamRouted = streamRouted;
        LoadEqState(eqConfigs, selectedEq);
        _suppressEvents = false;
    }

    /// <summary>
    /// Sonar側の値をポーリングで反映する（EQは対象外）。
    /// 直近でユーザーが操作したスライダーは、操作の反映がまだ届いていない可能性があるため上書きしない。
    /// </summary>
    public void RefreshClassicVolume(VolumeSettings vol)
    {
        var wasSuppressed = _suppressEvents;
        _suppressEvents = true;
        if (DateTime.UtcNow - _classicVolumeChangedAt > LocalChangeGracePeriod)
            ClassicVolume = vol.Volume;
        ClassicMuted = vol.Muted;
        _suppressEvents = wasSuppressed;
    }

    /// <summary>Streamer モードでのポーリング反映（EQは対象外）</summary>
    public void RefreshStreamerVolumes(
        VolumeSettings personal, VolumeSettings stream, bool personalRouted, bool streamRouted)
    {
        var wasSuppressed = _suppressEvents;
        _suppressEvents = true;
        if (DateTime.UtcNow - _personalVolumeChangedAt > LocalChangeGracePeriod)
            PersonalVolume = personal.Volume;
        PersonalMuted = personal.Muted;
        PersonalRouted = personalRouted;
        if (DateTime.UtcNow - _streamVolumeChangedAt > LocalChangeGracePeriod)
            StreamVolume = stream.Volume;
        StreamMuted = stream.Muted;
        StreamRouted = streamRouted;
        _suppressEvents = wasSuppressed;
    }

    private void LoadEqState(IReadOnlyList<EqConfig> eqConfigs, EqConfig? selectedEq)
    {
        EqConfigs.Clear();
        foreach (var eq in eqConfigs)
            EqConfigs.Add(eq);
        SelectedEqConfig = selectedEq;
    }
}
