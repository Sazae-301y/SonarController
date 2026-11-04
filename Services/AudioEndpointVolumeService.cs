using NAudio.CoreAudioApi;
using SonarController.Models;
using SonarController.Services.License;

namespace SonarController.Services;

/// <summary>Windowsの物理オーディオエンドポイント音量を操作する。</summary>
public interface IAudioEndpointVolumeService
{
    double GetVolume(AudioDevice device);
    void SetVolume(AudioDevice device, double volume);
}

public sealed class AudioEndpointVolumeService : IAudioEndpointVolumeService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly LicenseService _license;

    public AudioEndpointVolumeService(LicenseService license) => _license = license;

    public double GetVolume(AudioDevice device)
    {
        using var endpoint = GetEndpoint(device);
        return endpoint.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public void SetVolume(AudioDevice device, double volume)
    {
        _license.EnsureUsable();
        using var endpoint = GetEndpoint(device);
        endpoint.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(volume, 0.0, 1.0);
    }

    private MMDevice GetEndpoint(AudioDevice device)
    {
        var endpoint = _enumerator.GetDevice(device.Id);
        if (endpoint is null)
            throw new InvalidOperationException($"オーディオデバイスが見つかりません: {device.FriendlyName}");
        return endpoint;
    }

    public void Dispose() => _enumerator.Dispose();
}