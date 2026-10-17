// ⚠️ 重写实现（reimplementation）—— **不是**从受保护程序集还原出来的代码。
// 依据：官方客户端 SDK（massarolucien/Mass.Sdk，MIT）暴露的线协议契约（20 个端点 + SignalR 三事件）。
// 原始客户端里对应的类型（MassService / DesktopSession / NetEaseDesktop 等）方法体是 DNGuard 占位，
// 其语义不在任何可提取产物中；本文件是**按契约重新实现**，行为等价性未经验证。
namespace MassInfinity.Rewrite.Client;

using MassInfinity.Rewrite.Protocol;

/// <summary>Mass 本地服务（默认 <c>127.0.0.1:23333</c>）的客户端。</summary>
public sealed class MassClient : IDisposable
{
    public const int DefaultPort = 23333;
    private readonly HttpTransport _t;

    public MassClient(string baseUrl, HttpClient? http = null)
    {
        _t = new HttpTransport(baseUrl, http);
        Desktop = new DesktopEndpoints(this);
        Mobile = new MobileEndpoints(this);
        Instance = new InstanceEndpoints(this);
    }

    public string BaseUrl => _t.BaseUrl;

    public DesktopEndpoints Desktop { get; }
    public MobileEndpoints Mobile { get; }
    public InstanceEndpoints Instance { get; }

    /// <summary>按端口递增探测本地服务，命中第一个可 ping 通的实例。</summary>
    public static async Task<MassClient?> FindAsync(int startPort = DefaultPort, int tryTimes = 10, CancellationToken ct = default)
    {
        for (var i = 0; i < tryTimes; i++)
        {
            var c = new MassClient($"http://127.0.0.1:{startPort + i}");
            if (await c.PingAsync(ct).ConfigureAwait(false)) return c;
            c.Dispose();
        }
        return null;
    }

    internal Task<T> SendAsync<T>(string path, HttpMethod m, object? body, CancellationToken ct)
        => _t.SendAsync<T>(path, m, body, ct);

    internal Task SendAsync(string path, HttpMethod m, object? body, CancellationToken ct)
        => _t.SendAsync(path, m, body, ct);

    // ---- /api/base + /api/security ----

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try { await _t.SendAsync("/api/base/ping", HttpMethod.Get, null, ct).ConfigureAwait(false); return true; }
        catch { return false; }
    }

    /// <summary>用访问令牌换取会话（<c>POST /api/security/login</c>）。</summary>
    public Task SecurityLoginAsync(string token, CancellationToken ct = default)
        => _t.SendAsync("/api/security/login", HttpMethod.Post, new { token }, ct);

    /// <summary>订阅一次带进度的任务（SignalR）。<paramref name="url"/> 形如 <c>/api/desktop/launch/xxx</c>。</summary>
    public Task<GameInstanceDto> ProgressAsync(string url, IProgress<Progress>? callback = null, CancellationToken ct = default)
        => ProgressChannel.RunAsync(_t.BaseUrl, url, callback, ct);

    public void Dispose() => _t.Dispose();
}

/// <summary>进度流最后返回的实例描述（官方契约里的 GameInstance）。</summary>
public sealed class GameInstanceDto
{
    public string? GameId { get; set; }
    public string? RoleName { get; set; }
    public long InstanceId { get; set; }
}

/// <summary>桌面端登录（<c>/api/desktop/login/*</c>）。</summary>
public sealed class DesktopEndpoints
{
    private readonly MassClient _c;
    internal DesktopEndpoints(MassClient c) => _c = c;

    public Task<MassSession> LoginCookiesAsync(string cookies, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/cookies", HttpMethod.Post, new { cookies }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Cookies), ct);

    public Task<MassSession> Login163Async(string email, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/163", HttpMethod.Post, new { email, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Netease163), ct);

    public Task<MassSession> LoginMobileAsync(string mobile, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/mobile", HttpMethod.Post, new { mobile, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Mobile), ct);

    public Task SendSmsAsync(string mobile, CancellationToken ct = default)
        => _c.SendAsync("/api/desktop/login/mobile/send", HttpMethod.Post, new { mobile }, ct);

    public Task<MassSession> LoginSmsAsync(string mobile, string code, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/mobile/verify", HttpMethod.Post, new { mobile, code }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Mobile), ct);

    public Task<MassSession> Login4399PcAsync(string username, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/4399pc", HttpMethod.Post, new { username, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Pc4399), ct);

    public Task<MassSession> Login4399ComAsync(string username, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/4399com", HttpMethod.Post, new { username, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Com4399), ct);

    public Task<MassSession> Login4399ComRandomAsync(CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/desktop/login/random-4399com", HttpMethod.Post, null, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Desktop, AccountType.Com4399), ct);
}

/// <summary>手机端登录（<c>/api/mobile/login/*</c>）。</summary>
public sealed class MobileEndpoints
{
    private readonly MassClient _c;
    internal MobileEndpoints(MassClient c) => _c = c;

    public Task<MassSession> LoginCookiesAsync(string cookies, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/cookies", HttpMethod.Post, new { cookies }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Cookies), ct);

    public Task<MassSession> Login163Async(string email, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/163", HttpMethod.Post, new { email, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Netease163), ct);

    public Task<MassSession> LoginMobileAsync(string mobile, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/mobile", HttpMethod.Post, new { mobile, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Mobile), ct);

    public Task SendSmsAsync(string mobile, CancellationToken ct = default)
        => _c.SendAsync("/api/mobile/login/mobile/send", HttpMethod.Post, new { mobile }, ct);

    public Task<MassSession> LoginSmsAsync(string mobile, string code, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/mobile/verify", HttpMethod.Post, new { mobile, code }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Mobile), ct);

    public Task<MassSession> Login4399ComAsync(string username, string password, CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/4399com", HttpMethod.Post, new { username, password }, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Com4399), ct);

    public Task<MassSession> Login4399ComRandomAsync(CancellationToken ct = default)
        => _c.SendAsync<SessionDto>("/api/mobile/login/random-4399com", HttpMethod.Post, null, ct)
             .ContinueWith(t => MassSession.From(t.Result, AccountPlatform.Mobile, AccountType.Com4399), ct);
}

/// <summary>实例管理（<c>/api/instance/*</c>）。</summary>
public sealed class InstanceEndpoints
{
    private readonly MassClient _c;
    internal InstanceEndpoints(MassClient c) => _c = c;

    public Task<MassInstance[]> ListAsync(CancellationToken ct = default)
        => _c.SendAsync<MassInstance[]>("/api/instance/list", HttpMethod.Get, null, ct);

    public Task CloseAllAsync(CancellationToken ct = default)
        => _c.SendAsync("/api/instance/close-all", HttpMethod.Post, null, ct);

    public Task CloseAsync(string gameId, string roleName, CancellationToken ct = default)
        => _c.SendAsync("/api/instance/close", HttpMethod.Post, new { game_id = gameId, role_name = roleName }, ct);

    public Task CloseAsync(long instanceId, CancellationToken ct = default)
        => _c.SendAsync("/api/instance/close", HttpMethod.Post, new { instance_id = instanceId }, ct);
}
