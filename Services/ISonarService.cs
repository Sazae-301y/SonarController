using SonarController2.Models;

namespace SonarController2.Services;

/// <summary>
/// Sonar API の抽象化レイヤー
/// ViewModel はこのインターフェースのみを参照し、API の詳細を知らない
/// </summary>
public interface ISonarService
{
    // ---- Mode ----
    Task<SonarMode> GetModeAsync(CancellationToken ct = default);
    Task SetModeAsync(SonarMode mode, CancellationToken ct = default);

    // ---- Volume / Mute (Classic) ----
    Task<VolumeSettings> GetVolumeAsync(SonarChannel channel, CancellationToken ct = default);
    Task SetVolumeAsync(SonarChannel channel, double volume, CancellationToken ct = default);
    Task SetMuteAsync(SonarChannel channel, bool muted, CancellationToken ct = default);

    // ---- Volume / Mute (Streamer) ----
    Task<VolumeSettings> GetVolumeAsync(SonarChannel channel, SonarMix mix, CancellationToken ct = default);
    Task SetVolumeAsync(SonarChannel channel, SonarMix mix, double volume, CancellationToken ct = default);
    Task SetMuteAsync(SonarChannel channel, SonarMix mix, bool muted, CancellationToken ct = default);

    // ---- Routing (Streamer) ----
    /// <summary>
    /// 各チャンネルがPersonal/Streamの各ミックスへルーティングされているか（ミュートとは独立した設定）を一括取得する。
    /// Master チャンネルはルーティング対象外のためキーに含まれない。
    /// </summary>
    Task<IReadOnlyDictionary<(SonarChannel Channel, SonarMix Mix), bool>> GetRoutingStatusesAsync(CancellationToken ct = default);
    Task SetRoutingAsync(SonarChannel channel, SonarMix mix, bool enabled, CancellationToken ct = default);

    // ---- EQ Configs ----
    /// <summary>
    /// お気に入り登録済みの EQ プリセットを取得（isFavorite == true のみ）
    /// </summary>
    Task<IReadOnlyList<EqConfig>> GetFavoriteEqConfigsAsync(SonarChannel channel, CancellationToken ct = default);
    Task<EqConfig?> GetSelectedEqConfigAsync(SonarChannel channel, CancellationToken ct = default);
    Task SetEqConfigAsync(string configId, CancellationToken ct = default);

    // ---- Devices ----
    Task<IReadOnlyList<AudioDevice>> GetAudioDevicesAsync(CancellationToken ct = default);

    // ---- Streamer Mode ----
    Task<bool> GetAudienceMonitoringAsync(CancellationToken ct = default);
    Task SetAudienceMonitoringAsync(bool enabled, CancellationToken ct = default);

    // ---- Status ----
    Task<bool> IsSonarAvailableAsync();
}
