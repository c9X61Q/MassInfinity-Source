# `impl/` —— 重写层（**不是**还原出来的代码）

> ⚠️ **性质声明（先读这一条）**
> 这里的代码是**按公开契约重新实现**的，**不是**从 Mass Infinity 受保护程序集里还原出来的。
> 受 DNGuard HVM 保护的方法，其语义**不存在于任何可提取的产物中**（证据链见
> [过程仓库的文档](https://github.com/c9X61Q/MassInfinity-Reverse/blob/main/docs/VM-机制与下一步.md)）。
> 本层与原始程序集的关系是**平行实现**，不是「把原来的方法体填回去」。

---

## 覆盖什么

按**官方客户端 SDK**（`massarolucien/Mass.Sdk`，MIT；原文副本在两仓库的 `refs/`）暴露的线协议契约实现
**Mass 本地服务**（默认 `127.0.0.1:23333`）的客户端。

| 分组 | 端点 |
|---|---|
| base | `GET /api/base/ping` |
| security | `POST /api/security/login` |
| desktop 登录 | `cookies` / `163` / `mobile` / `mobile/send` / `mobile/verify` / `4399pc` / `4399com` / `random-4399com` |
| mobile 登录 | `cookies` / `163` / `mobile` / `mobile/send` / `mobile/verify` / `4399com` / `random-4399com` |
| instance | `GET /api/instance/list` / `POST /api/instance/close-all` / `POST /api/instance/close` |
| 进度流 | SignalR：`Progress(step,total,percentage,message)` / `GameInstance` / `Disposed` |

线协议细节（已按契约对齐）：**snake_case JSON**；统一信封 `{ code, msg, data }`，`code != 0` 抛 `MassApiException`。

## 不覆盖什么

- 协议背后的**服务端**行为（那是 Mass 服务本身，不在本仓库范围）。
- 原始客户端的 **UI 层**（`Mass.WinUI`）、**游戏协议层**（`Mass.Game.Proxy` / `Mass.Bedrock.Proxy` 的包编解码）、
  **启动器**（`Mass.Game.Launcher`）—— 这些的原始方法体同样是占位，本层不涉及。
- **行为等价性未验证**：本实现只保证「按契约发出正确的请求、按契约解析响应」，
  没有与原始客户端做过逐端点比对。

## 构建与用法

```bash
cd impl/MassInfinity.Rewrite && dotnet build     # net8.0，0 警告 0 错误
```

```csharp
using MassInfinity.Rewrite.Client;
using MassInfinity.Rewrite.Protocol;

// 探测本地服务
using var client = await MassClient.FindAsync();          // 从 23333 起递增探测
if (client is null) { Console.WriteLine("未找到运行中的 Mass 服务"); return; }

// 163 邮箱登录
var session = await client.Desktop.Login163Async("a@163.com", "password");
Console.WriteLine($"{session.Nickname} ({session.UserId})");

// 手机验证码登录
await client.Desktop.SendSmsAsync("13800000000");
var sms = await client.Desktop.LoginSmsAsync("13800000000", "123456");

// 实例列表 / 关闭
foreach (var inst in await client.Instance.ListAsync())
    Console.WriteLine($"{inst.Type} #{inst.Id} 启动于 {inst.LaunchTime:u}");
await client.Instance.CloseAsync(instanceId: 12345);

// 带进度的任务
var progress = new Progress<Progress>(p => Console.WriteLine($"[{p.Percentage}%] {p.Message}"));
var game = await client.ProgressAsync("/api/desktop/launch/xxx", progress);
```

## 文件

| 文件 | 内容 |
|---|---|
| `Protocol/Json.cs` | snake_case 序列化规则 |
| `Protocol/ApiEnvelope.cs` | `MassApiResponse` / `MassApiResponse<T>` / `MassApiException` |
| `Protocol/Dto.cs` | `SessionDto` / `AccountInfoDto` / `MassInstance` / `Progress` / `Page<T>` / 枚举 |
| `Client/HttpTransport.cs` | 传输：信封解包 + 错误统一化 |
| `Client/MassSession.cs` | 已登录会话 |
| `Client/MassClient.cs` | 客户端主体 + `Desktop` / `Mobile` / `Instance` 三组端点 |
| `Client/ProgressChannel.cs` | SignalR 进度流 |
