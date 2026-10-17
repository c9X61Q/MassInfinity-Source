// ⚠️ 重写实现 —— 不是还原代码。
// 配置结构直接复用「生成 DTO 层」（字段来自元数据，按构造正确）；
// Linux 专有的只有一件事：Path 必须是 Proton/Wine 视角的 Windows 路径。
namespace MassInfinity.Linux;

using System.Text.Json;
using System.Text.Encodings.Web;
using GG = MassInfinity.Rewrite.Generated.Mass_Game_Launcher.GGEN;

public sealed class LaunchRequest
{
    public string GameExe { get; set; }              // Windows 版基岩客户端 exe（在 prefix 里）
    public string GameDir { get; set; }              // 客户端目录（写进配置的 Path）
    public string WebServerUrl { get; set; } = "";
    public string CoreServerUrl { get; set; } = "";
    public string AuthServerUrl { get; set; } = "";
    public int LauncherPort { get; set; } = 19132;
    public string SensitiveWordFile { get; set; } = "";
    public string UserName { get; set; } = "";
    public uint UserId { get; set; }
    public string Urs { get; set; } = "";
    public string LevelId { get; set; } = "";
    public int GameType { get; set; } = 1;
    public int Difficulty { get; set; } = 1;
    public int PermissionLevel { get; set; } = 1;
    public bool Cheat { get; set; }
    public string WorldName { get; set; } = "Mass";
    public string RoomIp { get; set; } = "";
    public int RoomPort { get; set; }
    public string RoomName { get; set; } = "";
    public string RoomToken { get; set; } = "";
    public int RoomId { get; set; }
    public long HostId { get; set; }
    public bool ClosePetAddon { get; set; }
}

public static class LaunchConfigFactory
{
    public static GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_CppGameConfig Build(LaunchRequest r, Func<string, string> pathToWindows)
    {
        return new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_CppGameConfig
        {
            Path = pathToWindows(r.GameDir),
            WebServerUrl = r.WebServerUrl,
            CoreServerUrl = r.CoreServerUrl,
            LaunchParams = new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_LaunchParams { ClosePetAddon = r.ClosePetAddon },
            WorldInfo = new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_WorldInfo
            {
                LevelId = r.LevelId,
                Name = r.WorldName,
                GameType = r.GameType,
                Difficulty = r.Difficulty,
                PermissionLevel = r.PermissionLevel,
                Cheat = r.Cheat,
            },
            RoomInfo = new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_RoomInfo
            {
                Ip = r.RoomIp,
                Port = r.RoomPort,
                RoomName = r.RoomName,
                Token = r.RoomToken,
                RoomId = r.RoomId,
                HostId = r.HostId,
            },
            PlayerInfo = new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_PlayerInfo
            {
                UserId = r.UserId,
                UserName = r.UserName,
                Urs = r.Urs,
            },
            Misc = new GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_MiscInfo
            {
                AuthServerUrl = r.AuthServerUrl,
                LauncherPort = r.LauncherPort,
                SensitiveWordFile = r.SensitiveWordFile,
            },
            VipUsingMod = new List<string>(),
        };
    }

    // 配置里的键名沿用原结构（PascalCase），故不用 snake_case
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 中文直写，不转义成 \uXXXX
    };

    public static string ToJson(GG.G_Mass_Game_Launcher_Mass_Game_Launcher_Cpp_CppGameConfig c)
        => JsonSerializer.Serialize(c, Json);
}
