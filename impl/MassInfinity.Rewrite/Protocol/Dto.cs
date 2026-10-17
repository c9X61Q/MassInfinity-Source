// ⚠️ 重写实现 —— 不是还原代码。字段形状取自官方 SDK 契约。
namespace MassInfinity.Rewrite.Protocol;

public enum AccountType { Unknown = 0, Netease163 = 1, Mobile = 2, Cookies = 3, Pc4399 = 4, Com4399 = 5 }

public enum AccountPlatform { Unknown = 0, Desktop = 1, Mobile = 2 }

public class AccountInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public AccountType Type { get; set; }
    public AccountPlatform Platform { get; set; }
}

/// <summary>登录成功后服务端下发的会话。</summary>
public class SessionDto
{
    public string UserId { get; set; } = string.Empty;
    public string Cookies { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public AccountInfoDto Info { get; set; } = new();
}

public sealed record Progress(int Step, int Total, int Percentage, string Message);

public sealed class MassInstance
{
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
    public DateTime LaunchTime { get; set; }
}

public sealed class Page<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
}
