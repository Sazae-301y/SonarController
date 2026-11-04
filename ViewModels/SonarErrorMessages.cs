using SonarController.Helpers;
using SonarController.Services;

namespace SonarController.ViewModels;

/// <summary>Sonar接続エラーを、原因ごとのローカライズ済みメッセージに変換する。</summary>
internal static class SonarErrorMessages
{
    public static string Describe(Exception exception, LanguageService language) => exception switch
    {
        SonarNotAvailableException { Kind: SonarErrorKind.GgNotRunning } => language.GetString("SonarGgNotRunning"),
        SonarNotAvailableException { Kind: SonarErrorKind.SonarNotReady } => language.GetString("SonarNotReady"),
        SonarNotAvailableException => language.GetString("SonarConnectionLost"),
        SonarApiException api => string.Format(language.GetString("SonarApiError"), (int)api.StatusCode),
        _ => string.Format(language.GetString("ErrorPrefix"), exception.Message),
    };
}
