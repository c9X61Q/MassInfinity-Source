// ⚠️ 重写实现 —— 不是还原代码。
namespace MassInfinity.Rewrite.Client;

using System.Net.Http.Json;
using System.Text.Json;
using MassInfinity.Rewrite.Protocol;

/// <summary>把「信封 + snake_case」这套线协议规则收在一处。</summary>
internal sealed class HttpTransport
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public HttpTransport(string baseUrl, HttpClient? http = null)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        if (http is null) { _http = new HttpClient(); _ownsClient = true; }
        else _http = http;
        _http.BaseAddress ??= new Uri(BaseUrl + "/");
        _http.Timeout = TimeSpan.FromSeconds(100);
    }

    public string BaseUrl { get; }

    public async Task SendAsync(string path, HttpMethod method, object? body, CancellationToken ct)
        => await SendEnvelopeAsync<JsonElement?>(path, method, body, ct).ConfigureAwait(false);

    public async Task<T> SendAsync<T>(string path, HttpMethod method, object? body, CancellationToken ct)
    {
        var env = await SendEnvelopeAsync<T>(path, method, body, ct).ConfigureAwait(false);
        return env!;
    }

    private async Task<TData?> SendEnvelopeAsync<TData>(string path, HttpMethod method, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path.TrimStart('/'));
        if (body is not null)
            req.Content = JsonContent.Create(body, options: MassJson.Options);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        MassApiResponse<TData>? env;
        try
        {
            env = JsonSerializer.Deserialize<MassApiResponse<TData>>(raw, MassJson.Options);
        }
        catch (JsonException ex)
        {
            throw new MassApiException((int)resp.StatusCode, $"响应不是合法信封: {ex.Message}; body={Truncate(raw)}");
        }

        if (env is null) throw new MassApiException((int)resp.StatusCode, $"空响应; body={Truncate(raw)}");
        if (env.Code != 0) throw new MassApiException(env.Code, env.Msg);
        return env.Data;
    }

    private static string Truncate(string s, int n = 256) => s.Length <= n ? s : s[..n] + "…";

    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
