// ⚠️ 重写实现 —— 不是还原代码。帧结构取自 MC Bedrock LoginPacket 公开规范。
namespace MassInfinity.Rewrite.Bedrock;

using System.IO.Compression;
using System.Text;

/// <summary>
/// <c>LoginPacket</c>（packet id 0x01）：
/// <code>
/// i32(BE) protocol_version
/// string  connection_request   ← JWT 链（"header.payload.signature.header.payload.signature"）
/// string  skin_data            ← JWT，其 payload 段是 **zlib 压缩**的 ClientPayload JSON
/// </code>
/// </summary>
public sealed class LoginPacket
{
    public const int PacketId = 0x01;

    public int ProtocolVersion { get; set; }
    public string ConnectionRequest { get; set; } = string.Empty;
    public string SkinData { get; set; } = string.Empty;

    /// <summary>从皮肤 JWT 解出 client payload（zlib 解压 → JSON）。</summary>
    public ClientPayload ReadClientPayload()
    {
        var seg = Jwt.MiddleSegment(SkinData);
        var inflated = Jwt.InflateZlib(seg);
        return ClientPayload.FromJson(inflated);
    }

    /// <summary>把 client payload 写进皮肤 JWT 的 payload 段（JSON → zlib → base64url）。</summary>
    public LoginPacket WithClientPayload(ClientPayload payload, string headerJson = Jwt.DefaultHeader)
    {
        var deflated = Jwt.DeflateZlib(payload.ToJson());
        SkinData = Jwt.Assemble(headerJson, Jwt.UrlSafeB64(deflated));
        return this;
    }

    /// <summary>整包（不含包 id 前缀之外的帧头）序列化。</summary>
    public byte[] Encode(byte[]? extra = null)
        => new BedrockWriter()
            .I32Big(ProtocolVersion)
            .String(ConnectionRequest)
            .String(SkinData)
            .ToArray();

    public static LoginPacket Decode(byte[] frame)
    {
        var r = new BedrockReader(frame);
        var protocol = r.I32Big();
        var conn = r.String();
        var skin = r.String();
        return new LoginPacket { ProtocolVersion = protocol, ConnectionRequest = conn, SkinData = skin };
    }
}

/// <summary>JWT 拆装 + 皮肤 JWT 负载的 zlib 处理。</summary>
public static class Jwt
{
    public const string DefaultHeader = "{\"alg\":null,\"x5u\":null}";

    public static string UrlSafeB64(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromUrlSafeB64(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        switch (t.Length % 4) { case 2: t += "=="; break; case 3: t += "="; break; }
        return Convert.FromBase64String(t);
    }

    /// <summary>取 JWT 的中间段（payload）。链式 JWT 时取第一段的 payload。</summary>
    public static byte[] MiddleSegment(string jwt)
    {
        if (string.IsNullOrEmpty(jwt)) throw new InvalidDataException("空 JWT");
        var first = jwt.Split('.');
        if (first.Length < 2) throw new InvalidDataException("JWT 段数不足");
        return FromUrlSafeB64(first[1]);
    }

    public static string InflateZlib(byte[] data)
    {
        using var input = new MemoryStream(data, writable: false);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var outp = new MemoryStream();
        z.CopyTo(outp);
        return Encoding.UTF8.GetString(outp.ToArray());
    }

    public static byte[] DeflateZlib(string text)
    {
        using var outp = new MemoryStream();
        using (var z = new ZLibStream(outp, CompressionLevel.Optimal, leaveOpen: true))
        {
            var b = Encoding.UTF8.GetBytes(text);
            z.Write(b, 0, b.Length);
        }
        return outp.ToArray();
    }

    public static string Assemble(string headerJson, string payloadB64, string signatureB64 = "")
        => $"{UrlSafeB64(Encoding.UTF8.GetBytes(headerJson))}.{payloadB64}.{signatureB64}";
}
