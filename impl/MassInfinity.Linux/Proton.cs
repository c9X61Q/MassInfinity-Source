// ⚠️ 重写实现 —— 不是还原代码。
// Linux 等价物替换：Windows 侧「直接 CreateProcess 起游戏 exe」→ Linux 侧「经 Proton 起同一个 exe」。
// 其余（配置结构、服务协议）不动。
namespace MassInfinity.Linux;

using System.Diagnostics;

/// <summary>把「在 Linux 上启动 Windows 版基岩客户端」这件事收在一处。</summary>
public sealed class ProtonRunner
{
    public required string ProtonPath { get; init; }
    public required string PrefixPath { get; init; }   // WINEPREFIX
    public string SteamClientPath { get; init; } = ""; // STEAM_COMPAT_CLIENT_INSTALL_PATH
    public bool Verbose { get; init; }
    public bool DryRun { get; init; }
    public string LogPath { get; init; }

    /// <summary>按 Steam 的约定探测 Proton（环境变量 → Steam 常见安装位）。</summary>
    public static string DetectProton()
    {
        var env = Environment.GetEnvironmentVariable("PROTON") ?? Environment.GetEnvironmentVariable("PROTON_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
            "/usr/share/steam",
        };
        foreach (var r in roots)
        {
            var common = Path.Combine(r, "steamapps/common");
            if (!Directory.Exists(common)) continue;
            foreach (var d in Directory.EnumerateDirectories(common, "Proton*").OrderByDescending(x => x))
            {
                var p = Path.Combine(d, "proton");
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    /// <summary>把 Linux 绝对路径翻成 Proton 视角的 Windows 路径（<c>/home/x</c> → <c>Z:\home\x</c>，由 Wine 的 Z: 盘提供）。</summary>
    public static string ToWindowsPath(string linuxPath)
    {
        if (string.IsNullOrEmpty(linuxPath)) return linuxPath;
        var full = Path.GetFullPath(linuxPath);
        return "Z:" + full.Replace('/', '\\');
    }

    public List<string> BuildCommandLine(string gameExe, IEnumerable<string> gameArgs)
    {
        var argv = new List<string> { ProtonPath, "run", gameExe };
        argv.AddRange(gameArgs);
        return argv;
    }

    public Dictionary<string, string> BuildEnvironment()
    {
        var env = new Dictionary<string, string>
        {
            ["WINEPREFIX"] = PrefixPath,
            ["STEAM_COMPAT_DATA_PATH"] = Path.Combine(PrefixPath, "compatdata"),
            ["PROTON_USE_WINED3D"] = Environment.GetEnvironmentVariable("PROTON_USE_WINED3D") ?? "0",
        };
        if (!string.IsNullOrEmpty(SteamClientPath)) env["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = SteamClientPath;
        if (!string.IsNullOrEmpty(LogPath)) env["PROTON_LOG"] = "1";
        return env;
    }

    public int Run(string gameExe, IEnumerable<string> gameArgs)
    {
        var argv = BuildCommandLine(gameExe, gameArgs);
        var env = BuildEnvironment();

        if (DryRun)
        {
            Console.WriteLine("── dry-run：将要执行 ──");
            foreach (var kv in env) Console.WriteLine($"  env {kv.Key}={kv.Value}");
            Console.WriteLine("  " + string.Join(" ", argv.Select(Quote)));
            return 0;
        }

        Directory.CreateDirectory(PrefixPath);
        var psi = new ProcessStartInfo(argv[0]) { UseShellExecute = false };
        for (var i = 1; i < argv.Count; i++) psi.ArgumentList.Add(argv[i]);
        foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

        Console.WriteLine($"启动：{Quote(argv[0])} {string.Join(" ", argv.Skip(1).Select(Quote))}");
        using var proc = Process.Start(psi);
        if (proc is null) { Console.Error.WriteLine("启动失败：Process.Start 返回 null"); return 127; }
        proc.WaitForExit();
        return proc.ExitCode;
    }

    // 仅用于显示：已自带引号的参数不再二次包裹
    public static string Quote(string s)
    {
        if (s.Contains('"')) return s;
        return s.Contains(' ') ? "\"" + s + "\"" : s;
    }
}
