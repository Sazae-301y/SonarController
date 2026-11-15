using System.Globalization;
using System.Text.Json;
using SonarController.Models;
using SonarController.Services.License;

namespace SonarController.Services;

/// <summary>
/// ISonarService の実装。すべての API 呼び出しをここに集約する。
/// API 仕様が変わった場合はこのファイルだけ修正すれば良い。
/// </summary>
public class SonarService : ISonarService
{
    private static readonly SonarChannel[] AllChannels = Enum.GetValues<SonarChannel>();

    private readonly SonarHttpClient _http;
    private readonly SonarAddressResolver _resolver;
    private readonly LicenseService _license;

    public SonarService(SonarHttpClient http, SonarAddressResolver resolver, LicenseService license)
    {
        _http = http;
        _resolver = resolver;
        _license = license;
    }

    // ================== Status ==================

    public Task<bool> IsSonarAvailableAsync() => _resolver.IsSonarAvailableAsync();

    // ================== Mode ==================

    public async Task<SonarMode> GetModeAsync(CancellationToken ct = default)
    {
        // GET /mode  → "classic" or "stream"
        var raw = (await _http.GetStringAsync("mode", ct)).Trim('"', '\n', '\r', ' ');
        return raw == "stream" ? SonarMode.Streamer : SonarMode.Classic;
    }

    public async Task SetModeAsync(SonarMode mode, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /mode/classic  or  PUT /mode/stream
        var key = mode == SonarMode.Streamer ? "stream" : "classic";
        await _http.PutAsync($"mode/{key}", ct);
        // GGのバグ防止のため少し待つ
        await Task.Delay(150, ct);
    }

    // ================== Volume / Mute (Classic) ==================

    public async Task<VolumeSettings> GetVolumeAsync(SonarChannel channel, CancellationToken ct = default)
    {
        // GET /volumeSettings/classic/
        using var doc = await _http.GetAsync("volumeSettings/classic/", ct);
        var root = doc.RootElement;

        if (channel == SonarChannel.Master)
        {
            var master = root.GetProperty("masters").GetProperty("classic");
            return new VolumeSettings(
                master.GetProperty("volume").GetDouble(),
                master.GetProperty("muted").GetBoolean());
        }
        else
        {
            var device = root.GetProperty("devices").GetProperty(channel.ToJsonKey()).GetProperty("classic");
            return new VolumeSettings(
                device.GetProperty("volume").GetDouble(),
                device.GetProperty("muted").GetBoolean());
        }
    }

    public async Task<IReadOnlyDictionary<SonarChannel, VolumeSettings>> GetAllClassicVolumesAsync(
        CancellationToken ct = default)
    {
        using var doc = await _http.GetAsync("volumeSettings/classic/", ct);
        var root = doc.RootElement;
        var masters = root.GetProperty("masters").GetProperty("classic");
        var devices = root.GetProperty("devices");

        var result = new Dictionary<SonarChannel, VolumeSettings>(AllChannels.Length);
        foreach (var channel in AllChannels)
        {
            var entry = channel == SonarChannel.Master
                ? masters
                : devices.GetProperty(channel.ToJsonKey()).GetProperty("classic");
            result[channel] = new VolumeSettings(
                entry.GetProperty("volume").GetDouble(),
                entry.GetProperty("muted").GetBoolean());
        }

        return result;
    }

    public async Task SetVolumeAsync(SonarChannel channel, double volume, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /volumeSettings/classic/{channel}/Volume/{0.00}
        var vol = volume.ToString("0.00", CultureInfo.InvariantCulture);
        await _http.PutAsync($"volumeSettings/classic/{channel.ToHttpKey()}/Volume/{vol}", ct);
    }

    public async Task SetMuteAsync(SonarChannel channel, bool muted, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /volumeSettings/classic/{channel}/Mute/{true|false}
        var state = muted.ToString().ToLowerInvariant();
        await _http.PutAsync($"volumeSettings/classic/{channel.ToHttpKey()}/Mute/{state}", ct);
    }

    // ================== Volume / Mute (Streamer) ==================

    public async Task<VolumeSettings> GetVolumeAsync(SonarChannel channel, SonarMix mix, CancellationToken ct = default)
    {
        // GET /volumeSettings/streamer/
        using var doc = await _http.GetAsync("volumeSettings/streamer/", ct);
        var root = doc.RootElement;
        var mixKey = mix.ToHttpKey(); // "monitoring" or "streaming"

        if (channel == SonarChannel.Master)
        {
            var master = root.GetProperty("masters").GetProperty("stream").GetProperty(mixKey);
            return new VolumeSettings(
                master.GetProperty("volume").GetDouble(),
                master.GetProperty("muted").GetBoolean());
        }
        else
        {
            var device = root.GetProperty("devices").GetProperty(channel.ToJsonKey())
                             .GetProperty("stream").GetProperty(mixKey);
            return new VolumeSettings(
                device.GetProperty("volume").GetDouble(),
                device.GetProperty("muted").GetBoolean());
        }
    }

    public async Task<IReadOnlyDictionary<SonarChannel, (VolumeSettings Personal, VolumeSettings Stream)>> GetAllStreamerVolumesAsync(
        CancellationToken ct = default)
    {
        using var doc = await _http.GetAsync("volumeSettings/streamer/", ct);
        var root = doc.RootElement;
        var masters = root.GetProperty("masters").GetProperty("stream");
        var devices = root.GetProperty("devices");
        var personalKey = SonarMix.Personal.ToHttpKey();
        var streamKey = SonarMix.Stream.ToHttpKey();

        var result = new Dictionary<SonarChannel, (VolumeSettings Personal, VolumeSettings Stream)>(AllChannels.Length);
        foreach (var channel in AllChannels)
        {
            var entry = channel == SonarChannel.Master
                ? masters
                : devices.GetProperty(channel.ToJsonKey()).GetProperty("stream");
            var personalEl = entry.GetProperty(personalKey);
            var streamEl = entry.GetProperty(streamKey);

            result[channel] = (
                new VolumeSettings(personalEl.GetProperty("volume").GetDouble(), personalEl.GetProperty("muted").GetBoolean()),
                new VolumeSettings(streamEl.GetProperty("volume").GetDouble(), streamEl.GetProperty("muted").GetBoolean()));
        }

        return result;
    }

    public async Task SetVolumeAsync(SonarChannel channel, SonarMix mix, double volume, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /volumeSettings/streamer/{mix}/{channel}/volume/{0.00}
        var vol = volume.ToString("0.00", CultureInfo.InvariantCulture);
        await _http.PutAsync(
            $"volumeSettings/streamer/{mix.ToHttpKey()}/{channel.ToHttpKey()}/volume/{vol}", ct);
    }

    public async Task SetMuteAsync(SonarChannel channel, SonarMix mix, bool muted, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /volumeSettings/streamer/{mix}/{channel}/isMuted/{true|false}
        var state = muted.ToString().ToLowerInvariant();
        await _http.PutAsync(
            $"volumeSettings/streamer/{mix.ToHttpKey()}/{channel.ToHttpKey()}/isMuted/{state}", ct);
    }

    // ================== Routing (Streamer) ==================
    // GET /streamRedirections が返す streamRedirectionId は "monitoring"(Personal) / "streaming"(Stream)。
    // 各要素の status[] に role（channelのJSONキーと同じ）ごとの isEnabled が入っている。
    // Master にはロールが存在しないため対象外。

    public async Task<IReadOnlyDictionary<(SonarChannel Channel, SonarMix Mix), bool>> GetRoutingStatusesAsync(
        CancellationToken ct = default)
        => (await GetStreamRedirectionsAsync(ct)).RoutingStatuses;

    public async Task<StreamRedirectionsSnapshot> GetStreamRedirectionsAsync(CancellationToken ct = default)
    {
        using var doc = await _http.GetAsync("streamRedirections", ct);
        var routing = new Dictionary<(SonarChannel, SonarMix), bool>();
        string? personalDeviceId = null;
        string? streamDeviceId = null;
        string? micDeviceId = null;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var redirectionId = item.GetProperty("streamRedirectionId").GetString();
            var deviceId = item.TryGetProperty("deviceId", out var devIdProp) ? devIdProp.GetString() : null;
            switch (redirectionId)
            {
                case "monitoring":
                    personalDeviceId = deviceId;
                    break;
                case "streaming":
                    streamDeviceId = deviceId;
                    break;
                case "mic":
                    micDeviceId = deviceId;
                    continue;
                default:
                    continue;
            }

            var mix = redirectionId == "monitoring" ? SonarMix.Personal : SonarMix.Stream;
            if (!item.TryGetProperty("status", out var status)) continue;
            foreach (var role in status.EnumerateArray())
            {
                var roleKey = role.GetProperty("role").GetString();
                var channel = RoleKeyToChannel(roleKey);
                if (channel is null) continue;

                routing[(channel.Value, mix)] = role.GetProperty("isEnabled").GetBoolean();
            }
        }

        return new StreamRedirectionsSnapshot(routing, personalDeviceId, streamDeviceId, micDeviceId);
    }

    public async Task SetRoutingAsync(SonarChannel channel, SonarMix mix, bool enabled, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        if (channel == SonarChannel.Master)
            throw new ArgumentException("Master channel has no routing.", nameof(channel));

        var redirectionId = mix == SonarMix.Personal ? "monitoring" : "streaming";
        var state = enabled.ToString().ToLowerInvariant();
        // PUT streamRedirections/{monitoring|streaming}/redirections/{role}/isEnabled/{true|false}（実機のDevToolsで確認済み）
        await _http.PutAsync($"streamRedirections/{redirectionId}/redirections/{channel.ToJsonKey()}/isEnabled/{state}", ct);
    }

    private static SonarChannel? RoleKeyToChannel(string? roleKey) => roleKey switch
    {
        "game"        => SonarChannel.Game,
        "chatRender"  => SonarChannel.Chat,
        "media"       => SonarChannel.Media,
        "aux"         => SonarChannel.Aux,
        "chatCapture" => SonarChannel.Mic,
        _ => null
    };

    // ================== EQ Configs ==================

    public async Task<IReadOnlyList<EqConfig>> GetFavoriteEqConfigsAsync(
        SonarChannel channel, CancellationToken ct = default)
    {
        // GET /configs → 全EQ設定の配列
        using var doc = await _http.GetAsync("configs", ct);
        var channelKey = channel.ToJsonKey();

        var results = new List<EqConfig>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            // チャンネルが一致し、かつお気に入り登録済みのものだけ
            if (item.GetProperty("virtualAudioDevice").GetString() != channelKey) continue;
            if (!item.GetProperty("isFavorite").GetBoolean()) continue;

            results.Add(new EqConfig(
                Id:               item.GetProperty("id").GetString()!,
                Name:             item.GetProperty("name").GetString()!,
                Channel:          channel,
                IsFavorite:       true,
                FavoritePosition: item.GetProperty("favoritePosition").GetInt32()
            ));
        }

        // favoritePosition 順に並べる
        results.Sort((a, b) => a.FavoritePosition.CompareTo(b.FavoritePosition));
        return results;
    }

    public async Task<IReadOnlyDictionary<SonarChannel, IReadOnlyList<EqConfig>>> GetAllFavoriteEqConfigsAsync(
        CancellationToken ct = default)
    {
        using var doc = await _http.GetAsync("configs", ct);
        var grouped = new Dictionary<SonarChannel, List<EqConfig>>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.GetProperty("isFavorite").GetBoolean()) continue;

            var channel = RoleKeyToChannel(item.GetProperty("virtualAudioDevice").GetString());
            if (channel is null) continue;

            if (!grouped.TryGetValue(channel.Value, out var list))
            {
                list = [];
                grouped[channel.Value] = list;
            }

            list.Add(new EqConfig(
                Id:               item.GetProperty("id").GetString()!,
                Name:             item.GetProperty("name").GetString()!,
                Channel:          channel.Value,
                IsFavorite:       true,
                FavoritePosition: item.GetProperty("favoritePosition").GetInt32()
            ));
        }

        var result = new Dictionary<SonarChannel, IReadOnlyList<EqConfig>>(grouped.Count);
        foreach (var (channel, list) in grouped)
        {
            list.Sort((a, b) => a.FavoritePosition.CompareTo(b.FavoritePosition));
            result[channel] = list;
        }

        return result;
    }

    public async Task<EqConfig?> GetSelectedEqConfigAsync(SonarChannel channel, CancellationToken ct = default)
    {
        // GET /configs/selected → 現在適用中の設定
        using var doc = await _http.GetAsync("configs/selected", ct);
        var channelKey = channel.ToJsonKey();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.GetProperty("virtualAudioDevice").GetString() != channelKey) continue;

            // 選択中の config は isFavorite が false でも返す
            var isFav = item.TryGetProperty("isFavorite", out var fav) && fav.GetBoolean();
            var favPos = item.TryGetProperty("favoritePosition", out var fp) ? fp.GetInt32() : -1;

            return new EqConfig(
                Id:               item.GetProperty("id").GetString()!,
                Name:             item.GetProperty("name").GetString()!,
                Channel:          channel,
                IsFavorite:       isFav,
                FavoritePosition: favPos
            );
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<SonarChannel, EqConfig>> GetAllSelectedEqConfigsAsync(
        CancellationToken ct = default)
    {
        using var doc = await _http.GetAsync("configs/selected", ct);
        var result = new Dictionary<SonarChannel, EqConfig>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var channel = RoleKeyToChannel(item.GetProperty("virtualAudioDevice").GetString());
            if (channel is null || result.ContainsKey(channel.Value)) continue;

            var isFav = item.TryGetProperty("isFavorite", out var fav) && fav.GetBoolean();
            var favPos = item.TryGetProperty("favoritePosition", out var fp) ? fp.GetInt32() : -1;

            result[channel.Value] = new EqConfig(
                Id:               item.GetProperty("id").GetString()!,
                Name:             item.GetProperty("name").GetString()!,
                Channel:          channel.Value,
                IsFavorite:       isFav,
                FavoritePosition: favPos
            );
        }

        return result;
    }

    public async Task SetEqConfigAsync(string configId, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /configs/{configId}/select
        await _http.PutAsync($"configs/{configId}/select", ct);
    }

    // ================== Devices ==================

    public async Task<IReadOnlyList<AudioDevice>> GetAudioDevicesAsync(CancellationToken ct = default)
    {
        // GET /audioDevices
        using var doc = await _http.GetAsync("audioDevices", ct);
        var results = new List<AudioDevice>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dataFlowStr = item.GetProperty("dataFlow").GetString();
            // 不明な値はRender扱いにせず除外（入出力の取り違え防止）
            AudioDataFlow dataFlow;
            if (string.Equals(dataFlowStr, "capture", StringComparison.OrdinalIgnoreCase))
                dataFlow = AudioDataFlow.Capture;
            else if (string.Equals(dataFlowStr, "render", StringComparison.OrdinalIgnoreCase))
                dataFlow = AudioDataFlow.Render;
            else
                continue;

            results.Add(new AudioDevice(
                Id:           item.GetProperty("id").GetString()!,
                FriendlyName: item.GetProperty("friendlyName").GetString()!,
                DataFlow:     dataFlow,
                IsVirtual:    item.TryGetProperty("isVad", out var vad) && vad.ValueKind == JsonValueKind.True,
                Role:         item.TryGetProperty("role", out var role) ? role.GetString() ?? "" : ""
            ));
        }

        return results.AsReadOnly();
    }

    public async Task<AudioDevice?> GetSelectedDeviceAsync(
        AudioDataFlow dataFlow, SonarMix? mix = null, CancellationToken ct = default)
    {
        var devices = await GetAudioDevicesAsync(ct);
        using var doc = await _http.GetAsync("streamRedirections", ct);
        var redirectionId = mix switch
        {
            SonarMix.Personal => "monitoring",
            SonarMix.Stream => "streaming",
            _ when dataFlow == AudioDataFlow.Capture => "mic",
            _ => null
        };

        if (redirectionId is null)
            return null;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.GetProperty("streamRedirectionId").GetString() != redirectionId)
                continue;

            var deviceId = item.GetProperty("deviceId").GetString();
            return devices.FirstOrDefault(d => d.DataFlow == dataFlow && d.Id == deviceId);
        }

        return null;
    }

    public async Task SetSelectedDeviceAsync(
        AudioDevice device, SonarMix? mix = null, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // マイク(mix=null)は入力、個人/ストリームミックスは出力デバイスのみ許可
        var expected = mix is null ? AudioDataFlow.Capture : AudioDataFlow.Render;
        if (device.DataFlow != expected)
            throw new ArgumentException("デバイスの種類(入力/出力)が用途と一致しません。", nameof(device));

        var scope = mix switch
        {
            SonarMix.Personal => "monitoring",
            SonarMix.Stream => "streaming",
            _ => "mic"
        };
        await _http.PutAsync(
            $"streamRedirections/{scope}/deviceId/{Uri.EscapeDataString(device.Id)}", ct);
    }

    // ================== Audience Monitoring ==================

    public async Task<bool> GetAudienceMonitoringAsync(CancellationToken ct = default)
    {
        // GET /streamRedirections/isStreamMonitoringEnabled → bool
        using var doc = await _http.GetAsync("streamRedirections/isStreamMonitoringEnabled", ct);
        return doc.RootElement.GetBoolean();
    }

    public async Task SetAudienceMonitoringAsync(bool enabled, CancellationToken ct = default)
    {
        _license.EnsureUsable();
        // PUT /streamRedirections/isStreamMonitoringEnabled/{true|false}
        await _http.PutAsync(
            $"streamRedirections/isStreamMonitoringEnabled/{enabled.ToString().ToLowerInvariant()}", ct);
    }
}
