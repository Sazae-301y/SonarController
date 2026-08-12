using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SonarController2.Models;
using SonarController2.Services;
using System.Collections.ObjectModel;

namespace SonarController2.ViewModels;

/// <summary>
/// 設定ウィンドウ内のデバイス一覧セクション用 ViewModel（読み取り専用）
/// </summary>
public partial class DevicesViewModel : ViewModelBase
{
    private readonly ISonarService _sonar;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<AudioDevice> OutputDevices { get; } = new();
    public ObservableCollection<AudioDevice> InputDevices { get; } = new();

    public DevicesViewModel(ISonarService sonar)
    {
        _sonar = sonar;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        OutputDevices.Clear();
        InputDevices.Clear();

        try
        {
            var devices = await _sonar.GetAudioDevicesAsync();
            foreach (var d in devices)
            {
                if (d.DataFlow == AudioDataFlow.Render)
                    OutputDevices.Add(d);
                else
                    InputDevices.Add(d);
            }
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
}
