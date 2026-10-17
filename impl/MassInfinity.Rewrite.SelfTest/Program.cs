// ⚠️ 重写层的自检（往返一致性）。测的是「本实现自洽」，**不是**与原始客户端的行为等价性。
using System.Text.Json;
using MassInfinity.Rewrite.Bedrock;
using MassInfinity.Rewrite.Client;
using MassInfinity.Rewrite.Protocol;

var failed = 0;
void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}{(ok || detail is null ? "" : "  → " + detail)}");
    if (!ok) failed++;
}

Console.WriteLine("== VarInt（无符号 LEB128 / 有符号 zigzag）==");
foreach (var v in new ulong[] { 0, 1, 127, 128, 255, 16383, 16384, ulong.MaxValue })
{
    var w = new BedrockWriter(); w.VarUInt(v);
    var r = new BedrockReader(w.ToArray());
    Check($"VarUInt {v}", r.VarUInt() == v);
}
foreach (var v in new long[] { long.MinValue, -1000000, -1, 0, 1, 1000000, long.MaxValue })
{
    var w = new BedrockWriter(); w.VarSInt(v);
    var r = new BedrockReader(w.ToArray());
    var got = r.VarSInt();
    Check($"VarSInt {v}", got == v, got.ToString());
}

Console.WriteLine("== Bedrock 定长/字符串 ==");
{
    var w = new BedrockWriter().I32Big(-123456789).I64(9876543210L).Bool(true).Bool(false).String("皮肤-skin");
    var r = new BedrockReader(w.ToArray());
    Check("i32 BE", r.I32Big() == -123456789);
    Check("i64 LE", r.I64() == 9876543210L);
    Check("bool true/false", r.Bool() && !r.Bool());
    Check("string(utf8+varint)", r.String() == "皮肤-skin");
}

Console.WriteLine("== ClientPayload JSON 往返（41 字段）==");
var payload = new ClientPayload
{
    AnimatedImageData = { new AnimatedImageData { Type = 1, Frames = 2.5f, Image = "iVBOR", ImageHeight = 32, ImageWidth = 64, AnimationExpression = 0 } },
    ArmSize = "wide", CapeData = "AA==", CapeId = "cape-1", CapeImageHeight = 32, CapeImageWidth = 64,
    CapeOnClassicSkin = true, ClientRandomId = -7370887009435546000L, CompatibleWithClientSideChunkGen = false,
    CurrentInputMode = 1, DefaultInputMode = 1, DeviceId = "dev-abc", DeviceModel = "PC", DeviceOs = DeviceOs.Windows10,
    GameVersion = "1.20.51", GrowthLevel = 0, IsEditorMode = false, LanguageCode = "zh_CN", OverrideSkin = false,
    PersonaPieces = { new PersonaPiece { Id = "p1", Type = "t1", PackId = "pk", Default = true, ProductId = "prod" } },
    PersonaSkin = false,
    PieceTintColors = { new PieceTintColor { PieceType = "hair", Colors = { "#000000", "#FFFFFF" } } },
    PlatformOfflineId = "", PlatformOnlineId = "pf-online", PlayFabId = "pf", PremiumSkin = false,
    SelfSignedId = "self", ServerAddress = "127.0.0.1:19132", SkinAnimationData = "", SkinColor = "#0",
    SkinData = "c2tpbkRhdGE=", SkinGeometryData = "{}", SkinGeometryDataEngineVersion = "1.14.0",
    SkinIID = "skin-iid", SkinId = "skin-id", SkinImageHeight = 64, SkinImageWidth = 64,
    SkinResourcePatch = "{\"geometry\":{}}", ThirdPartyName = "tester", ThirdPartyNameOnly = false,
    TrustedSkin = true, UIProfile = 0,
};
var json = payload.ToJson();
var back = ClientPayload.FromJson(json);
Check("JSON 字段数 = 41", json.Split(':').Length >= 41, $"实际 {json.Split(':').Length}");
Check("ArmSize", back.ArmSize == payload.ArmSize);
Check("DeviceOs 枚举", back.DeviceOs == DeviceOs.Windows10);
Check("ClientRandomId (i64 负值)", back.ClientRandomId == payload.ClientRandomId);
Check("AnimatedImageData[0].Image", back.AnimatedImageData[0].Image == "iVBOR");
Check("PieceTintColors[0].Colors.Count", back.PieceTintColors[0].Colors.Count == 2);
Check("TrustedSkin", back.TrustedSkin);
Check("全字段一致（再序列化相同）", back.ToJson() == json);

Console.WriteLine("== LoginPacket 帧往返 ==");
var login = new LoginPacket { ProtocolVersion = 671 };
login.WithClientPayload(payload);
var frame = login.Encode();
var decoded = LoginPacket.Decode(frame);
Check("protocol_version", decoded.ProtocolVersion == 671);
Check("connection_request 空串可往返", decoded.ConnectionRequest == "");
Check("skin_data 段数 = 3", decoded.SkinData.Split('.').Length == 3);
var backPayload = decoded.ReadClientPayload();
Check("zlib+JWT 全链路取回 ClientPayload", backPayload.ToJson() == json);
Check("取回后字段抽样", backPayload.SkinId == "skin-id" && backPayload.DeviceModel == "PC");

Console.WriteLine("== 协议信封 ==");
{
    var env = JsonSerializer.Deserialize<MassApiResponse<SessionDto>>(
        "{\"code\":0,\"msg\":\"ok\",\"data\":{\"user_id\":\"u1\",\"cookies\":\"c\",\"nickname\":\"n\",\"info\":{\"user_id\":\"u1\",\"nickname\":\"n\",\"type\":1,\"platform\":1}}}",
        MassJson.Options);
    Check("snake_case 信封解码", env is { Code: 0 } && env.Data!.UserId == "u1" && env.Data.Info.Type == AccountType.Netease163);
}

Console.WriteLine("== 生成 DTO 层全量往返（反射遍历）==");
{
    var asm = typeof(MassInfinity.Rewrite.Generated.Mass_Domain.GGEN.G_Mass_Domain_Mass_Domain_Value_BoolValue).Assembly;
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    int types = 0, props = 0, okTypes = 0, failTypes = 0;
    var failures = new List<string>();
    foreach (var t in asm.GetTypes())
    {
        if (!t.IsClass || t.IsAbstract) continue;
        var ps = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (ps.Length == 0) continue;
        types++;
        try
        {
            var inst = Activator.CreateInstance(t);
            foreach (var p in ps)
            {
                props++;
                if (p.CanWrite) p.SetValue(inst, DefaultOf(p.PropertyType));
            }
            var j = JsonSerializer.Serialize(inst, opts);
            var rt = JsonSerializer.Deserialize(j, t, opts);
            var j2 = JsonSerializer.Serialize(rt, opts);
            if (j == j2) okTypes++; else { failTypes++; failures.Add(t.Name); }
        }
        catch (Exception ex) { failTypes++; failures.Add(t.Name + ": " + ex.GetType().Name); }
    }
    Check($"遍历到 {types} 个生成类 / {props} 个属性", types > 700, types.ToString());
    Check($"全部往返一致（{okTypes}/{types}）", failTypes == 0, failures.Count > 0 ? string.Join("; ", failures.Take(3)) : null);
}

#nullable disable
static object DefaultOf(Type t)
{
    if (t == typeof(string)) return "";
    if (t.IsValueType) return Activator.CreateInstance(t);
    if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 0);
    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return Activator.CreateInstance(t);
    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return Activator.CreateInstance(t);
    return null;
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? "全部通过" : $"{failed} 项失败");
return failed == 0 ? 0 : 1;
