using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SonarController.Helpers;
using SonarController.Models;
using SonarController.Services;
using System.Collections.ObjectModel;

namespace SonarController.ViewModels;

/// <summary>
/// 設定ウィンドウ内のデバイス一覧セクション用 ViewModel（読み取り専用）
/// </summary>
public partial class DevicesViewModel : ViewModelBase
{
    private readonly ISonarService _sonar;
    private readonly LanguageService _language;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<AudioDevice> OutputDevices { get; } = new();
    public ObservableCollection<AudioDevice> InputDevices { get; } = new();

    public DevicesViewModel(ISonarService sonar, LanguageService language)
    {
        _sonar = sonar;
        _language = language;
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
            ErrorMessage = string.Format(_language.GetString("ErrorPrefix"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
