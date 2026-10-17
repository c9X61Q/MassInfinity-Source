// ⚠️ 重写实现 —— 不是还原代码。
// 字段名与顺序来自：① 反编译骨架里 Mass.Bedrock.Proxy.ClientPayload 的元数据声明（41 个字段）；
// ② MC Bedrock LoginPacket 皮肤 JWT 的公开 JSON 结构。两者一致。
// JSON 键为 PascalCase（Bedrock 皮肤 JWT 的实际规则），**不走** snake_case。
namespace MassInfinity.Rewrite.Bedrock;

using System.Text.Json;
using System.Text.Json.Serialization;

public enum DeviceOs
{
    Unknown = -1, Android = 1, Ios = 2, Osx = 3, FireOs = 4, GearVr = 5, Hololens = 6,
    Windows10 = 7, Windows = 8, Dedicated = 9, Tvos = 10, Playstation = 11, Nintendo = 12,
    Xbox = 13, WindowsPhone = 14,
}

public sealed class AnimatedImageData
{
    public int Type { get; set; }
    public float Frames { get; set; }
    public string Image { get; set; } = string.Empty;
    public int ImageHeight { get; set; }
    public int ImageWidth { get; set; }
    public int AnimationExpression { get; set; }
}

public sealed class PersonaPiece
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public bool Default { get; set; }
    public string ProductId { get; set; } = string.Empty;
}

public sealed class PieceTintColor
{
    public string PieceType { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = new();
}

/// <summary>
/// MC Bedrock <c>LoginPacket</c> 皮肤 JWT 里的 client payload（第三段 JWT 解压后的 JSON）。
/// 41 个字段的名称/类型取自反编译元数据，排列顺序与元数据一致。
/// </summary>
public sealed class ClientPayload
{
    public List<AnimatedImageData> AnimatedImageData { get; set; } = new();
    public string ArmSize { get; set; } = string.Empty;
    public string CapeData { get; set; } = string.Empty;
    public string CapeId { get; set; } = string.Empty;
    public int CapeImageHeight { get; set; }
    public int CapeImageWidth { get; set; }
    public bool CapeOnClassicSkin { get; set; }
    public long ClientRandomId { get; set; }
    public bool CompatibleWithClientSideChunkGen { get; set; }
    public int CurrentInputMode { get; set; }
    public int DefaultInputMode { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public DeviceOs DeviceOs { get; set; }
    public string GameVersion { get; set; } = string.Empty;
    public int GrowthLevel { get; set; }
    public bool IsEditorMode { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public bool OverrideSkin { get; set; }
    public List<PersonaPiece> PersonaPieces { get; set; } = new();
    public bool PersonaSkin { get; set; }
    public List<PieceTintColor> PieceTintColors { get; set; } = new();
    public string PlatformOfflineId { get; set; } = string.Empty;
    public string PlatformOnlineId { get; set; } = string.Empty;
    public string PlayFabId { get; set; } = string.Empty;
    public bool PremiumSkin { get; set; }
    public string SelfSignedId { get; set; } = string.Empty;
    public string ServerAddress { get; set; } = string.Empty;
    public string SkinAnimationData { get; set; } = string.Empty;
    public string SkinColor { get; set; } = string.Empty;
    public string SkinData { get; set; } = string.Empty;
    public string SkinGeometryData { get; set; } = string.Empty;
    public string SkinGeometryDataEngineVersion { get; set; } = string.Empty;
    public string SkinIID { get; set; } = string.Empty;
    public string SkinId { get; set; } = string.Empty;
    public int SkinImageHeight { get; set; }
    public int SkinImageWidth { get; set; }
    public string SkinResourcePatch { get; set; } = string.Empty;
    public string ThirdPartyName { get; set; } = string.Empty;
    public bool ThirdPartyNameOnly { get; set; }
    public bool TrustedSkin { get; set; }
    public int UIProfile { get; set; }

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static ClientPayload FromJson(string json)
        => JsonSerializer.Deserialize<ClientPayload>(json, Json) ?? new ClientPayload();
}
