using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Text.Json;

namespace SonarController2.Services;

/// <summary>
/// SteelSeries GG の coreProps.json を読み取り、Sonar API の WebServerAddress を解決する
/// </summary>
public class SonarAddressResolver
{
    private const string CorePropsPath =
        @"C:\ProgramData\SteelSeries\GG\coreProps.json";
    private static readonly TimeSpan StartupWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly HttpClient Http = CreateHttpClient();

    private string? _cachedAddress;
    private bool _launchAttempted;

    /// <summary>
    /// Sonar API のベースURL（例: http://localhost:12268）を取得する
    /// GG が起動していない場合は null を返す
    /// </summary>
    public async Task<string?> GetWebServerAddressAsync()
    {
        if (_cachedAddress is not null)
            return _cachedAddress;

        var address = await TryResolveAddressAsync();
        if (address is not null)
            return _cachedAddress = address;

        if (!_launchAttempted)
        {
            _launchAttempted = true;
            TryStartSteelSeriesGg();
        }

        var deadline = DateTime.UtcNow + StartupWaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(StartupRetryDelay);
            address = await TryResolveAddressAsync();
            if (address is not null)
            {
                _cachedAddress = address;
                return address;
            }
        }

        return null;
    }

    private static async Task<string?> TryResolveAddressAsync()
    {
        if (!File.Exists(CorePropsPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(CorePropsPath);
            using var doc = JsonDocument.Parse(json);

            // coreProps.json に ggEncryptedAddress がある -> HTTPS で /subApps を呼ぶ
            if (!doc.RootElement.TryGetProperty("ggEncryptedAddress", out var encAddr))
                return null;

            var ggAddr = encAddr.GetString();
            if (string.IsNullOrEmpty(ggAddr))
                return null;

            var subAppsUrl = $"https://{ggAddr}/subApps";
            return await FetchSonarAddressFromSubAppsAsync(subAppsUrl);
        }
        catch
        {
            return null;
        }
    }

    private static void TryStartSteelSeriesGg()
    {
        if (Process.GetProcessesByName("SteelSeriesGG").Length > 0)
            return;

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SteelSeries", "GG", "SteelSeriesGG.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SteelSeries", "GG", "SteelSeriesGG.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelSeries", "GG", "SteelSeriesGG.exe")
        };

        var executablePath = candidates.FirstOrDefault(File.Exists);
        if (executablePath is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
            // 起動できない場合は、通常の Sonar API 接続エラーとして扱う
        }
    }

    /// <summary>
    /// GG の /subApps エンドポイントから Sonar の webServerAddress を取得する
    /// </summary>
    private static async Task<string?> FetchSonarAddressFromSubAppsAsync(string url)
    {
        try
        {
            var response = await Http.GetStringAsync(url);
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

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// キャッシュをクリアして次回再解決させる（GG 再起動後などに使用）
    /// </summary>
    public void InvalidateCache()
    {
        _cachedAddress = null;
        _launchAttempted = false;
    }

    /// <summary>
    /// Sonar が現在利用可能か（アドレス取得できるか）を確認する
    /// </summary>
    public async Task<bool> IsSonarAvailableAsync()
        => await GetWebServerAddressAsync() is not null;
}
