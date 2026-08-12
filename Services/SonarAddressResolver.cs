using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace SonarController2.Services;

/// <summary>
/// SteelSeries GG の coreProps.json を読み取り、Sonar API の WebServerAddress を解決する
/// </summary>
public class SonarAddressResolver
{
    private const string CorePropsPath =
        @"C:\ProgramData\SteelSeries\GG\coreProps.json";

    private string? _cachedAddress;

    /// <summary>
    /// Sonar API のベースURL（例: http://localhost:12268）を取得する
    /// GG が起動していない場合は null を返す
    /// </summary>
    public async Task<string?> GetWebServerAddressAsync()
    {
        if (_cachedAddress is not null)
            return _cachedAddress;

        if (!File.Exists(CorePropsPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(CorePropsPath);
            using var doc = JsonDocument.Parse(json);

            // coreProps.json に ggEncryptedAddress がある → HTTPS で /subApps を呼ぶ
            if (doc.RootElement.TryGetProperty("ggEncryptedAddress", out var encAddr))
            {
                var ggAddr = encAddr.GetString();
                if (!string.IsNullOrEmpty(ggAddr))
                {
                    var subAppsUrl = $"https://{ggAddr}/subApps";
                    var sonarAddr = await FetchSonarAddressFromSubAppsAsync(subAppsUrl);
                    if (sonarAddr is not null)
                    {
                        _cachedAddress = sonarAddr;
                        return _cachedAddress;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// GG の /subApps エンドポイントから Sonar の webServerAddress を取得する
    /// </summary>
    private static async Task<string?> FetchSonarAddressFromSubAppsAsync(string url)
    {
        try
        {
            // 自己署名証明書を許可
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

            var response = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("subApps", out var subApps) &&
                subApps.TryGetProperty("sonar", out var sonar) &&
                sonar.TryGetProperty("metadata", out var meta) &&
                meta.TryGetProperty("webServerAddress", out var addr))
            {
                var address = addr.GetString();
                if (!string.IsNullOrEmpty(address))
                    return address.TrimEnd('/');
            }
        }
        catch
        {
            // GG が起動していない、または接続失敗
        }

        return null;
    }

    /// <summary>
    /// キャッシュをクリアして次回再解決させる（GG 再起動後などに使用）
    /// </summary>
    public void InvalidateCache() => _cachedAddress = null;

    /// <summary>
    /// Sonar が現在利用可能か（アドレス取得できるか）を確認する
    /// </summary>
    public async Task<bool> IsSonarAvailableAsync()
        => await GetWebServerAddressAsync() is not null;
}
