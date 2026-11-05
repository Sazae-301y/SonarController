using System.Diagnostics;

namespace SonarController.Helpers;

/// <summary>製品版の購入ページ(Microsoft Store)を既定のブラウザで開く。</summary>
public static class PurchaseLink
{
    public const string Url = "https://apps.microsoft.com/detail/restricted/9NTNDW7WDZCF";

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
