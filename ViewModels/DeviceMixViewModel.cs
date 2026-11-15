using CommunityToolkit.Mvvm.ComponentModel;
using SonarController.Helpers;
using SonarController.Models;
using SonarController.Services;
using System.Collections.ObjectModel;

namespace SonarController.ViewModels;

/// <summary>ミックス単位のデバイス選択とデバイスマスター音量を管理する。</summary>
public partial class DeviceMixViewModel : ViewModelBase
{
    private readonly ISonarService _sonar;
    private readonly IAudioEndpointVolumeService _endpointVolume;
    private readonly SonarMix? _mix;
    private readonly bool _isInput;
    private readonly LanguageService _language;
    private readonly string _displayNameKey;
    private bool _suppressEvents;
    private bool _isRefreshingDevices;

    public string DisplayName => _language.GetString(_displayNameKey);
    public AudioDataFlow DataFlow => _isInput ? AudioDataFlow.Capture : AudioDataFlow.Render;
    public bool IsDisconnected => SelectedDevice is null;
    public ObservableCollection<AudioDevice> Devices { get; } = new();

    [ObservableProperty]
    private AudioDevice? _selectedDevice;

    [ObservableProperty]
    private double _volume;

    public DeviceMixViewModel(
        ISonarService sonar,
        IAudioEndpointVolumeService endpointVolume,
        string displayNameKey,
        SonarMix? mix,
        bool isInput,
        LanguageService language)
    {
        _sonar = sonar;
        _endpointVolume = endpointVolume;
        _displayNameKey = displayNameKey;
        _mix = mix;
        _isInput = isInput;
        _language = language;
        _language.LanguageChanged += (_, _) => OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnSelectedDeviceChanged(AudioDevice? value)
    {
        OnPropertyChanged(nameof(IsDisconnected));
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

    private bool IsSelectable(AudioDevice d)
    {
        if (d.DataFlow != DataFlow) return false;
        if (!d.IsVirtual) return true;
        // 仮想デバイスは、ストリームミックスの出力先に Sonar Stream のみ許可（マイクと個人ミックスは不可）
        return !_isInput && _mix == SonarMix.Stream && d.Role == "stream";
    }

    public void Load(IReadOnlyList<AudioDevice> devices, string? selectedDeviceId)
    {
        _suppressEvents = true;
        Devices.Clear();
        foreach (var device in devices.Where(IsSelectable))
            Devices.Add(device);

        SelectedDevice = selectedDeviceId is null
            ? null
            : Devices.FirstOrDefault(device => device.Id == selectedDeviceId);
        Volume = SelectedDevice is null ? 0.0 : _endpointVolume.GetVolume(SelectedDevice);
        _suppressEvents = false;
    }

    public async Task LoadAsync(IReadOnlyList<AudioDevice> devices, CancellationToken ct = default)
    {
        var selectedDevice = await _sonar.GetSelectedDeviceAsync(DataFlow, _mix, ct);
        Load(devices, selectedDevice?.Id);
    }

    public async Task RefreshDevicesAsync(CancellationToken ct = default)
    {
        if (_isRefreshingDevices) return;

        _isRefreshingDevices = true;
        try
        {
            var devices = await _sonar.GetAudioDevicesAsync(ct);
            var selectedDeviceId = SelectedDevice?.Id;

            _suppressEvents = true;
            try
            {
                Devices.Clear();
                foreach (var device in devices.Where(IsSelectable))
                    Devices.Add(device);

                var selectedDevice = selectedDeviceId is null
                    ? null
                    : Devices.FirstOrDefault(device => device.Id == selectedDeviceId);
                SelectedDevice = null;
                SelectedDevice = selectedDevice;
                if (selectedDevice is null)
                    Volume = 0.0;
            }
            finally
            {
                _suppressEvents = false;
            }
        }
        finally
        {
            _isRefreshingDevices = false;
        }
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
