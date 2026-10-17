// ⚠️ 重写实现 —— 不是还原代码。
namespace MassInfinity.Rewrite.Protocol;

/// <summary>统一应答信封：<c>{ "code": int, "msg": string, "data": T }</c>。</summary>
public class MassApiResponse
{
    public int Code { get; set; }
    public string Msg { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class MassApiResponse<TData> : MassApiResponse
{
    public TData? Data { get; set; }
}

/// <summary>当信封 <c>code != 0</c> 时抛出。</summary>
public sealed class MassApiException : Exception
{
    public int Code { get; }
    public MassApiException(int code, string msg) : base($"Mass API {code}: {msg}") => Code = code;
}
