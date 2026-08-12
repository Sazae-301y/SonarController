namespace SonarController2.Models;

/// <summary>
/// Sonar のオーディオチャンネル
/// </summary>
public enum SonarChannel
{
    Master,
    Game,
    Chat,
    Media,
    Aux,
    Mic
}

public static class SonarChannelExtensions
{
    /// <summary>
    /// API の HTTP パスキーへ変換（volumeSettings のパス用）
    /// </summary>
    public static string ToHttpKey(this SonarChannel channel) => channel switch
    {
        SonarChannel.Master => "Master",
        SonarChannel.Game   => "game",
        SonarChannel.Chat   => "chatRender",
        SonarChannel.Media  => "media",
        SonarChannel.Aux    => "aux",
        SonarChannel.Mic    => "chatCapture",
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    /// <summary>
    /// JSON レスポンス内の devices キーへ変換
    /// </summary>
    public static string ToJsonKey(this SonarChannel channel) => channel switch
    {
        SonarChannel.Game  => "game",
        SonarChannel.Chat  => "chatRender",
        SonarChannel.Media => "media",
        SonarChannel.Aux   => "aux",
        SonarChannel.Mic   => "chatCapture",
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    public static string ToDisplayName(this SonarChannel channel) => channel switch
    {
        SonarChannel.Master => "Master",
        SonarChannel.Game   => "Game",
        SonarChannel.Chat   => "Chat",
        SonarChannel.Media  => "Media",
        SonarChannel.Aux    => "Aux",
        SonarChannel.Mic    => "Mic",
        _ => channel.ToString()
    };
}
