namespace SonarController2.Models;

/// <summary>
/// Windowsの再生/録音デバイス（/audioDevices から取得）
/// </summary>
public record AudioDevice(
    string Id,
    string FriendlyName,
    AudioDataFlow DataFlow
);

public enum AudioDataFlow
{
    Render,  // 出力デバイス（スピーカー、ヘッドセット等）
    Capture  // 入力デバイス（マイク等）
}
