using CommunityToolkit.Mvvm.ComponentModel;
using SonarController2.Models;
using SonarController2.Services;
using System.Collections.ObjectModel;

namespace SonarController2.ViewModels;

/// <summary>ミックス単位のデバイス選択とデバイスマスター音量を管理する。</summary>
public partial class DeviceMixViewModel : ViewModelBase
{
    private readonly ISonarService _sonar;
    private readonly IAudioEndpointVolumeService _endpointVolume;
    private readonly SonarMix? _mix;
    private readonly bool _isInput;
    private bool _suppressEvents;

    public string DisplayName { get; }
    public AudioDataFlow DataFlow => _isInput ? AudioDataFlow.Capture : AudioDataFlow.Render;
    public ObservableCollection<AudioDevice> Devices { get; } = new();

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private double _volume;

    public DeviceMixViewModel(
        ISonarService sonar,
        IAudioEndpointVolumeService endpointVolume,
        string displayName,
        SonarMix? mix,
        bool isInput)
    {
        _sonar = sonar;
        _endpointVolume = endpointVolume;
        DisplayName = displayName;
        _mix = mix;
        _isInput = isInput;
    }

    partial void OnSelectedDeviceChanged(AudioDevice? value)
    {
        if (_suppressEvents || value is null) return;
        _ = SetDeviceAsync(value);
        _suppressEvents = true;
        Volume = _endpointVolume.GetVolume(value);
        _suppressEvents = false;
    }

    partial void OnVolumeChanged(double value)
    {
        if (_suppressEvents) return;
        SetVolume(Math.Clamp(value, 0.0, 1.0));
    }

    public async Task LoadAsync(IReadOnlyList<AudioDevice> devices, CancellationToken ct = default)
    {
        _suppressEvents = true;
        Devices.Clear();
        foreach (var device in devices.Where(d => d.DataFlow == DataFlow))
            Devices.Add(device);

        SelectedDevice = await _sonar.GetSelectedDeviceAsync(DataFlow, _mix, ct)
            ?? Devices.FirstOrDefault();
        Volume = SelectedDevice is null ? 0.0 : _endpointVolume.GetVolume(SelectedDevice);
        _suppressEvents = false;
    }

    private async Task SetDeviceAsync(AudioDevice device)
    {
        try { await _sonar.SetSelectedDeviceAsync(device, _mix); }
        catch { }
    }

    private void SetVolume(double value)
    {
        if (SelectedDevice is null) return;
        try { _endpointVolume.SetVolume(SelectedDevice, value); }
        catch { }
    }
}
