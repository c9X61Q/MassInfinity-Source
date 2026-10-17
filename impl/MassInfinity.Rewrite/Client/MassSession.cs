// ⚠️ 重写实现 —— 不是还原代码。
namespace MassInfinity.Rewrite.Client;

using MassInfinity.Rewrite.Protocol;

/// <summary>一条已登录会话：服务端下发的 <see cref="SessionDto"/> + 本地补充的元数据。</summary>
public sealed class MassSession
{
    public required string UserId { get; init; }
    public required string Cookies { get; init; }
    public required string Nickname { get; init; }
    public required AccountInfoDto Info { get; init; }
    public AccountPlatform Platform { get; init; }
    public AccountType LoginKind { get; init; }

    public string ToCookieHeader() => Cookies;

    public static MassSession From(SessionDto dto, AccountPlatform platform, AccountType kind) => new()
    {
        UserId = dto.UserId,
        Cookies = dto.Cookies,
        Nickname = dto.Nickname,
        Info = dto.Info,
        Platform = platform,
        LoginKind = kind,
    };
}
