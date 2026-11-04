using NAudio.CoreAudioApi;
using SonarController.Models;

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

    public double GetVolume(AudioDevice device)
    {
        using var endpoint = GetEndpoint(device);
        return endpoint.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public void SetVolume(AudioDevice device, double volume)
    {
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