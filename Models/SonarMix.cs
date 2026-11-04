namespace SonarController.Models;

/// <summary>
/// Streamer Mode のミックス（Personal / Stream）
/// </summary>
public enum SonarMix
{
    /// <summary>自分が聴く方（monitoring）</summary>
    Personal,
    /// <summary>配信先が聴く方（streaming）</summary>
    Stream
}

public static class SonarMixExtensions
{
    /// <summary>API のパスキーへ変換</summary>
    public static string ToHttpKey(this SonarMix mix) => mix switch
    {
        SonarMix.Personal => "monitoring",
        SonarMix.Stream   => "streaming",
        _ => throw new ArgumentOutOfRangeException(nameof(mix))
    };

    public static string ToDisplayName(this SonarMix mix) => mix switch
    {
        SonarMix.Personal => "Personal Mix",
        SonarMix.Stream   => "Stream Mix",
        _ => mix.ToString()
    };
}
