using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace SonarController2.Services;

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
        var url = await BuildUrlAsync(path);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// GET リクエストを送信し文字列を返す（単純な値向け）
    /// </summary>
    public async Task<string> GetStringAsync(string path, CancellationToken ct = default)
    {
        var url = await BuildUrlAsync(path);
        return await _http.GetStringAsync(url, ct);
    }

    /// <summary>
    /// PUT リクエストを送信する（ボディなし）
    /// </summary>
    public async Task PutAsync(string path, CancellationToken ct = default)
    {
        var url = await BuildUrlAsync(path);
        var content = new StringContent(string.Empty);
        content.Headers.ContentLength = 0;
        var response = await _http.PutAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> BuildUrlAsync(string path)
    {
        var baseUrl = await _resolver.GetWebServerAddressAsync()
            ?? throw new SonarNotAvailableException("Sonar API に接続できません。SteelSeries GG が起動していることを確認してください。");
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

/// <summary>
/// Sonar が利用できないときにスローされる例外
/// </summary>
public class SonarNotAvailableException : Exception
{
    public SonarNotAvailableException(string message) : base(message) { }
}
