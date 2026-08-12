namespace SonarController2.Models;

/// <summary>
/// 1チャンネルの音量・ミュート状態
/// </summary>
public record VolumeSettings(double Volume, bool Muted);
