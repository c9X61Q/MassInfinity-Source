// Mass Infinity Linux 启动器（⚠️ 重写实现，不是还原代码）
// 设计原则：**最小改动** —— Windows 专有的东西只换成 Linux 等价物：
//   ① 启动游戏：Windows 直接起 exe → Linux 经 Proton 起同一个 exe
//   ② 路径：Linux 绝对路径 → Proton/Wine 视角的 Z:\ 路径（写进配置的 Path 字段）
//   ③ 服务通信：不变（本地 HTTP + SignalR，跨平台）
//   ④ 配置结构：直接复用「生成 DTO 层」（字段来自元数据）
using MassInfinity.Linux;
using MassInfinity.Rewrite.Client;
using MassInfinity.Rewrite.Protocol;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Help(); return 0; }
    var cmd = args[0];
    var opt = ParseOpts(args.Skip(1).ToArray());

    switch (cmd)
    {
        case "ping":
        {
            var (url, _) = ServiceUrl(opt);
            using var c = new MassClient(url);
            Console.WriteLine(await c.PingAsync() ? $"服务在线：{url}" : $"服务不可达：{url}");
            return 0;
        }

        case "login163":
        {
            var (url, _) = ServiceUrl(opt);
            if (!opt.TryGetValue("email", out var email) || !opt.TryGetValue("password", out var pw))
            { Console.Error.WriteLine("需要 --email 与 --password"); return 2; }
            using var c = new MassClient(url);
            var s = await c.Desktop.Login163Async(email, pw);
            Console.WriteLine($"登录成功：{s.Nickname} ({s.UserId}) platform={s.Platform} kind={s.LoginKind}");
            return 0;
        }

        case "list":
        {
            var (url, _) = ServiceUrl(opt);
            using var c = new MassClient(url);
            var all = await c.Instance.ListAsync();
            if (all.Length == 0) Console.WriteLine("没有运行中的实例");
            foreach (var i in all) Console.WriteLine($"  {i.Type} #{i.Id}  user={i.UserId}  启动于 {i.LaunchTime:u}");
            return 0;
        }

        case "proton":
        {
            var p = ProtonRunner.DetectProton();
            Console.WriteLine(p is null ? "未检测到 Proton（可用 --proton 指定，或设 PROTON / PROTON_PATH）" : $"Proton：{p}");
            var z = ProtonRunner.ToWindowsPath("/home/user/game");
            Console.WriteLine($"路径换算示例：/home/user/game → {z}");
            return p is null ? 1 : 0;
        }

        case "launch":
            return RunLaunch(opt);

        default:
            Console.Error.WriteLine($"未知命令：{cmd}"); Help(); return 2;
    }
}

static int RunLaunch(Dictionary<string, string> opt)
{
    var proton = opt.GetValueOrDefault("proton") ?? ProtonRunner.DetectProton();
    if (proton is null) { Console.Error.WriteLine("未找到 Proton；用 --proton <路径> 指定，或设 PROTON。"); return 1; }

    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var prefix = opt.GetValueOrDefault("prefix") ?? Path.Combine(home, ".local/share/massinfinity/prefix");
    var gameDir = opt.GetValueOrDefault("game-dir") ?? Path.Combine(home, ".local/share/massinfinity/cppgame");
    // 安装布局对齐 fantnel：<游戏目录>/windowsmc/Minecraft.Windows.exe
    var gameExe = opt.GetValueOrDefault("game-exe") ?? Path.Combine(gameDir, "windowsmc", "Minecraft.Windows.exe");

    var req = new LaunchRequest
    {
        GameExe = gameExe,
        GameDir = gameDir,
        WebServerUrl = opt.GetValueOrDefault("web-url") ?? "",
        CoreServerUrl = opt.GetValueOrDefault("core-url") ?? "",
        AuthServerUrl = opt.GetValueOrDefault("auth-url") ?? "",
        LauncherPort = int.TryParse(opt.GetValueOrDefault("launcher-port"), out var lp) ? lp : 19132,
        SensitiveWordFile = opt.GetValueOrDefault("sensitive-word-file") ?? "",
        UserName = opt.GetValueOrDefault("user") ?? "",
        UserId = uint.TryParse(opt.GetValueOrDefault("uid"), out var uid) ? uid : 0u,
        Urs = opt.GetValueOrDefault("urs") ?? "",
        LevelId = opt.GetValueOrDefault("level-id") ?? "",
        RoomIp = opt.GetValueOrDefault("room-ip") ?? "",
        RoomPort = int.TryParse(opt.GetValueOrDefault("room-port"), out var rp) ? rp : 0,
        RoomName = opt.GetValueOrDefault("room-name") ?? "",
        RoomToken = opt.GetValueOrDefault("room-token") ?? "",
        RoomId = int.TryParse(opt.GetValueOrDefault("room-id"), out var rid) ? rid : 0,
        ClosePetAddon = opt.ContainsKey("close-pet-addon"),
    };

    var runner = new ProtonRunner
    {
        ProtonPath = proton,
        PrefixPath = prefix,
        SteamClientPath = opt.GetValueOrDefault("steam-client") ?? DetectSteam(),
        DryRun = opt.ContainsKey("dry-run"),
        Verbose = opt.ContainsKey("verbose"),
        LogPath = opt.GetValueOrDefault("proton-log") ?? "",
    };

    var cfg = LaunchConfigFactory.Build(req, ProtonRunner.ToWindowsPath);
    var json = LaunchConfigFactory.ToJson(cfg);
    var cfgOut = opt.GetValueOrDefault("config-out") ?? Path.Combine(gameDir, "launch.cppconfig");   // 文件名对齐 fantnel
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cfgOut))!);
    File.WriteAllText(cfgOut, json);
    Console.WriteLine($"已写出启动配置：{cfgOut}（{json.Length} 字节）");

    // ✅ CLI 形参已对齐：` config="<路径>"`（依据 fantnel CommandService.StartGame）。
    //    --game-args 仍可覆盖，{config} 会被替换为配置路径。
    var gameArgs = new List<string>();
    var template = opt.GetValueOrDefault("game-args");
    if (!string.IsNullOrEmpty(template))
        gameArgs.AddRange(template.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Replace("{config}", ProtonRunner.ToWindowsPath(cfgOut))));
    else
        gameArgs.Add($"config=\"{ProtonRunner.ToWindowsPath(cfgOut)}\"");

    var rc = runner.Run(gameExe, gameArgs);
    if (!runner.DryRun) Console.WriteLine($"游戏退出码：{rc}");
    return rc;
}

static (string url, int port) ServiceUrl(Dictionary<string, string> opt)
{
    var port = int.TryParse(opt.GetValueOrDefault("port"), out var p) ? p : MassClient.DefaultPort;
    return (opt.GetValueOrDefault("service") ?? $"http://127.0.0.1:{port}", port);
}

static string DetectSteam()
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    foreach (var r in new[] { Path.Combine(home, ".steam/steam"), Path.Combine(home, ".local/share/Steam"), "/usr/share/steam" })
        if (Directory.Exists(r)) return r;
    return "";
}

static Dictionary<string, string> ParseOpts(string[] a)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < a.Length; i++)
    {
        if (!a[i].StartsWith("--")) continue;
        var k = a[i][2..];
        if (i + 1 < a.Length && !a[i + 1].StartsWith("--")) d[k] = a[++i];
        else d[k] = "";
    }
    return d;
}

static void Help()
{
    Console.WriteLine("""
Mass Infinity Linux 启动器（重写实现，不是还原代码）

用法：massinfinity <命令> [选项]

命令：
  ping                        探测本地 Mass 服务（默认 127.0.0.1:23333）
  login163 --email E --password P    163 邮箱登录（走重写层协议客户端）
  list                        列出运行中的实例
  proton                      探测 Proton 与路径换算
  launch                      经 Proton 启动基岩客户端

launch 选项：
  --game-exe PATH             基岩客户端 exe（必填，Windows 版）
  --game-dir PATH             客户端目录（写进配置 Path；默认取 exe 所在目录）
  --proton PATH               Proton 可执行文件（默认自动探测 / $PROTON）
  --prefix PATH               WINEPREFIX（默认 ~/.local/share/massinfinity/prefix）
  --config-out PATH           启动配置输出路径
  --game-args "..."           客户端命令行模板，{config} 会被替换为配置路径
  --web-url/--core-url/--auth-url URL        服务地址
  --user NAME --uid N --urs TOKEN            玩家信息
  --level-id ID --room-ip IP --room-port P --room-name N --room-token T --room-id N
  --dry-run                   只打印将要执行的命令，不启动
""");
}
