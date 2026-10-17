// ⚠️ 重写实现（reimplementation）—— 不是从受保护程序集还原出来的代码。
// 依据：官方客户端 SDK（massarolucien/Mass.Sdk，MIT）的线协议契约。
namespace MassInfinity.Rewrite.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>线协议 JSON 规则：snake_case（服务端契约）。</summary>
public static class MassJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
