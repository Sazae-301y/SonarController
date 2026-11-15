using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SonarController.Helpers;

namespace SonarController.Services;

/// <summary>
/// Sonar ローカル API への低レベル HTTP クライアント
/// </summary>
public class SonarHttpClient : IDisposable
{
    private readonly SonarAddressResolver _resolver;
    private readonly HttpClient _http;
    private bool _disposed;

    public SonarHttpClient(SonarAddressResolver resolver)
    {
        _resolver = resolver;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// GET リクエストを送信し JsonDocument を返す
    /// </summary>
    public async Task<JsonDocument> GetAsync(string path, CancellationToken ct = default)
    {
        return await SendWithAddressRefreshAsync(async url =>
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }, path, ct);
    }

    /// <summary>
    /// GET リクエストを送信し文字列を返す（単純な値向け）
    /// </summary>
    public async Task<string> GetStringAsync(string path, CancellationToken ct = default)
    {
        return await SendWithAddressRefreshAsync(url => _http.GetStringAsync(url, ct), path, ct);
    }

    /// <summary>
    /// PUT リクエストを送信する（ボディなし）
    /// </summary>
    public async Task PutAsync(string path, CancellationToken ct = default)
    {
        await SendWithAddressRefreshAsync(async url =>
        {
            using var content = new StringContent(string.Empty);
            content.Headers.ContentLength = 0;
            using var response = await _http.PutAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            return true;
        }, path, ct);
    }

    private async Task<T> SendWithAddressRefreshAsync<T>(Func<string, Task<T>> request, string path, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var url = await BuildUrlAsync(path);

            try
            {
                return await request(url);
            }
            catch (Exception ex) when (IsConnectionFailure(ex, ct))
            {
                // GG再起動などでポートが変わった可能性があるため、アドレスを再解決して1回だけ再試行する
                AppLog.Warn($"Sonar API connection failed (attempt {attempt + 1}): {ex.GetType().Name}");
                if (attempt > 0)
                {
                    throw new SonarNotAvailableException(
                        SonarErrorKind.ConnectionLost,
                        "Sonar API に接続できません。SteelSeries GG と Sonar が起動していることを確認してください。", ex);
                }

                _resolver.InvalidateCache();
            }
            catch (HttpRequestException ex) when (ex.StatusCode is { } status)
            {
                AppLog.Warn($"Sonar API returned HTTP {(int)status} for {path}");
                throw new SonarApiException(status, ex);
            }
        }
    }

    private static bool IsConnectionFailure(Exception exception, CancellationToken ct) => exception switch
    {
        HttpRequestException { StatusCode: null } => true,
        TaskCanceledException when !ct.IsCancellationRequested => true,
        _ => false,
    };

    private async Task<string> BuildUrlAsync(string path)
    {
        var baseUrl = await _resolver.GetWebServerAddressAsync();
        if (baseUrl is null)
        {
            var kind = _resolver.LastFailure;
            AppLog.Warn($"Sonar address could not be resolved: {kind}");
            throw new SonarNotAvailableException(kind,
                kind == SonarErrorKind.SonarNotReady
                    ? "Sonar が起動していません。SteelSeries GG で Sonar を有効にしてください。"
                    : "Sonar API に接続できません。SteelSeries GG が起動していることを確認してください。");
        }

        return $"{baseUrl}/{path.TrimStart('/')}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>Sonar に接続できない理由の区分。</summary>
public enum SonarErrorKind
{
    /// <summary>SteelSeries GG が起動していない。</summary>
    GgNotRunning,

    /// <summary>GG は起動しているが Sonar が利用可能になっていない。</summary>
    SonarNotReady,

    /// <summary>以前は接続できたが通信に失敗した（GG の再起動など）。</summary>
    ConnectionLost,
}

/// <summary>
/// Sonar に接続できないときにスローされる例外
/// </summary>
public class SonarNotAvailableException : Exception
{
    public SonarErrorKind Kind { get; }

    public SonarNotAvailableException(SonarErrorKind kind, string message) : base(message)
        => Kind = kind;

    public SonarNotAvailableException(SonarErrorKind kind, string message, Exception innerException)
        : base(message, innerException) => Kind = kind;
}

/// <summary>Sonar には接続できたが、API が エラー応答を返したときの例外。</summary>
public class SonarApiException(HttpStatusCode statusCode, Exception innerException)
    : Exception($"Sonar API returned HTTP {(int)statusCode}.", innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
