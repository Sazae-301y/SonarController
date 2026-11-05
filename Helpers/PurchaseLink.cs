using System.Diagnostics;

namespace SonarController.Helpers;

/// <summary>製品版の購入ページ(Microsoft Store)を既定のブラウザで開く。</summary>
public static class PurchaseLink
{
    public const string Url = "https://apps.microsoft.com/store/detail/9NTNDW7WDZCF?cid=DevShareMCLPCS";

    public static void Open()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn("Failed to open the purchase page.", ex);
        }
    }
}
