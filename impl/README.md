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

## 第二增量：Bedrock 包层（`Bedrock/`）

`Mass.Bedrock.Proxy.ClientPayload` 是 **MC Bedrock `LoginPacket` 皮肤 JWT 里的 client payload**：
字段名与顺序取自反编译元数据的**声明**（41 个字段），结构与公开的 Bedrock 协议规范一致 —— 有契约可依，故可直接实现。

| 文件 | 内容 |
|---|---|
| `Bedrock/VarInt.cs` | 无符号 LEB128 + 有符号 zigzag 变长整数 |
| `Bedrock/BedrockBinary.cs` | 读写原语（i32 大端 / i64 小端 / bool / VarInt 长字符串） |
| `Bedrock/ClientPayload.cs` | 41 字段 + `DeviceOs` / `AnimatedImageData` / `PersonaPiece` / `PieceTintColor`（JSON 为 **PascalCase**，即 Bedrock 皮肤 JWT 的真实规则） |
| `Bedrock/LoginPacket.cs` | 帧结构 `i32(BE) protocol_version` + `string connection_request` + `string skin_data`；JWT 拆装 + 皮肤负载 **zlib** 解压/压缩 |

```csharp
var login = new LoginPacket { ProtocolVersion = 671 }.WithClientPayload(payload);
var bytes = login.Encode();                    // 发出去的帧
var back  = LoginPacket.Decode(bytes).ReadClientPayload();   // 取回 ClientPayload
```

### 自检

```bash
cd impl/MassInfinity.Rewrite.SelfTest && dotnet run     # 33 项，全部 PASS
```

覆盖：VarInt 边界（`0`/`127`/`128`/`ulong.MaxValue`/`long.MinValue`…）、定长与字符串、
41 字段 JSON 往返、LoginPacket 帧往返、**zlib+JWT 全链路**取回、协议信封 snake_case 解码、
以及**生成 DTO 层的全量反射往返**（720 个类 / 3,193 个属性，逐个 JSON 往返一致）。

> ⚠️ 自检测的是「**本实现自洽**」，不是与原始客户端的行为等价性 —— 后者无法验证（原始方法体不存在）。

## 第三增量：生成 DTO 层（`impl/MassInfinity.Rewrite.Generated/`）

**为什么这层可以"机械确定"**：属性访问器（`get_`/`set_`）的方法体**由编译器生成** —— 元数据里的
属性声明就完全决定了语义，不需要反编译。所以这层是**按构造正确**，不是猜。

```
780 个类型/枚举 → 3,193 个属性 → 11,959 行
覆盖 11 个程序集的**全部属性面**（Mass.Domain / Mass.Game.Proxy / Mass.WinUI / Mass.Bedrock.Proxy …）
```

- 生成器：过程仓库 `tools/MassDtoGen`（dnlib 读元数据，可复现）
- 枚举值取自 `Constant` 表；容器类型按泛型元数映射（`List<>` / `Dictionary<>` / `HashSet<>` / `Queue<>` / `Stack<>` / `Nullable`…）
- 保真度：**3,193 个属性中 344 个（10.8%）因原类型未生成而保守降级为 `object`**，已在生成文件注释里保留原类型全名
- ⚠️ 该层**只含属性与枚举**，**不含方法体**（混淆名方法无语义可写，见下）

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
