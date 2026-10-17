# Mass Infinity 源码（反编译还原）

> ### ⬇️ Linux 包：[`linux-v0.1 / pkg.tar.gz`](https://github.com/c9X61Q/MassInfinity-Source/releases/download/linux-v0.1/pkg.tar.gz)
> 32 MB，自包含（含 .NET 8 运行时）。解包后 `./massinfinity --help`；基岩客户端经 **Proton** 启动。

> **Mass Infinity `Mass.WinUI 1.35.107.1217`** 的反编译/还原源码。
> 逆向过程、证据链与取证数据集在另一个仓库：[`MassInfinity-Reverse`](https://github.com/c9X61Q/MassInfinity-Reverse)。

许可证：**GPL-3.0**（本仓库不含任何原始二进制）。

---

## 一、这个仓库里有什么

| 目录 | 内容 | 编译状态 |
|---|---|---|
| **`skeleton/`** | **11 个程序集的完整声明骨架** —— 用 dnlib 从元数据直接生成，不依赖反编译文本。体一律显式占位 | ✅ **0 错误** |
| **`reconstructed/`** | **110 个「无桩」反编译文件** + 3 个占位 shim —— 这部分是反编译器真正还原出来的代码 | ✅ **0 错误** |
| **`src/`** | 反编译器对 **1,191 个文件**的原始输出（约 23.4M 字符），其中约 14% 可读 | —— 参考材料 |
| **`impl/`** | **重写层**：① 服务协议客户端（20 端点 + SignalR）② Bedrock 包层（`ClientPayload` 41 字段 + `LoginPacket`）③ **生成 DTO 层**（780 类型 / 3,193 属性）④ **Linux 启动器**（经 Proton 起基岩客户端）。⚠️ **不是还原代码** | ✅ **0 错误**，自检 **33 项全通过** |

### 骨架规模

| 程序集 | 顶层类型 | 方法 |
|---|---|---|
| Mass.Common | 43 | 696 |
| Mass.Domain | 60 | 1,525 |
| Mass.Domain.Desktop | 112 | 3,280 |
| Mass.Domain.Mobile | 36 | 1,174 |
| Mass.Application | 62 | 1,254 |
| Mass.Application.Desktop | 34 | 1,242 |
| Mass.Application.Mobile | 15 | 374 |
| Mass.Game.Proxy | 482 | 11,284 |
| Mass.Game.Launcher | 91 | 3,866 |
| Mass.Bedrock.Proxy | 116 | 2,889 |
| Mass.WinUI | 158 | 16,758 |
| **合计** | **1,209** | **44,342** |

---

## 二、怎么构建

```bash
cd skeleton
dotnet build NonWinUI.csproj     # 10 个非 WinUI 程序集 —— 0 警告 0 错误
cd winui && dotnet build WinUI.csproj   # Mass.WinUI（WinUI3）
```

两者都能在 **Linux** 上构建（WinUI 工程只需 `EnableWindowsTargeting` + 关闭两个 Windows 二进制构建步骤：
`cswinrt.exe` 投影生成、`MakePri.exe` PRI 生成 —— 骨架既不定义 WinRT 类型也没有资源/XAML，这两步用不上）。

`reconstructed/`：

```bash
cd reconstructed && dotnet build
```

---

## 三、⚠️ 必须知道的三条边界

### 1. 骨架里 76.5% 的方法体是**占位**，不是还原

运行时方法里约 **88.4%** 被 **DNGuard HVM** 保护。这些方法的语义
**不存在于任何可提取的产物中** —— 五条独立证据链（桩体模板化、唯一的 IL 生成器每次运行只被调用约 12 次、
描述符表 16 槽全部解出且不含方法语义、原生入口 `sub_180001310` 只做一次比较后返回 0、
`.hvm0` 3MB 磁盘与内存逐字节一致且零引用），外加两项运行时实验（nativeEntry 覆写实验、
跑原版程序抓加密块可写副本）都指向同一结论。

⇒ **那部分只能「重写」，不能「还原」。** 本仓库里凡占位体都写着
`throw new global::System.NotImplementedException("未还原 (DNGuard HVM)")`，绝不含糊。

逐成员缺口台账（43,789 条：桩 33,514 = 76.5% / 真实 10,275 = 23.5%）在
[`MassInfinity-Reverse/data/gap-ledger.tsv.gz`](https://github.com/c9X61Q/MassInfinity-Reverse/blob/main/data/gap-ledger.tsv.gz)。

### 2. 为「可编译」而放弃的声明信息（全部可从元数据查回）

| 放弃项 | 原因 |
|---|---|
| 基类列表 | 否则派生成员遮蔽基类抽象成员（CS0533/CS0534） |
| 类/结构体的接口列表 | 否则报未实现接口成员 |
| 泛型 `where` 约束 | 否则类型实参不满足约束（CS0311） |
| 事件 → 字段 | 否则 `add_`/`remove_` 保留名冲突（CS0082） |
| `const` → `static readonly` | 否则常量字段缺值（CS0145） |
| 嵌套类型一律 `public` | 否则可访问性不一致（CS0051） |

### 3. 缺口占位（**不是官方实现**）

`skeleton/shims/` 下的文件是为可编译而写的同占位，覆盖项目输入里不存在的依赖：

| shim | 覆盖 |
|---|---|
| `GlobalGapShims.cs` | 全局命名空间的 `ValueCollection<TKey,TValue>`（MsgPack.Cli 的类型在元数据里未带命名空间限定） |
| `MatrixGap.cs` | `Matrix.Core.*`（独立第三方程序集，不在分析输入内） |
| `WinRTGap.cs` | `ComInterfaceEntry`（CsWinRT 编译期生成） |
| `CommunityToolkitGap.cs` | `CommunityToolkit.WinUI.Controls.{SettingsCard,SettingsExpander}` |

`reconstructed/shims/` 下另有三件（`InterfaceConverter.cs` / `Optional.cs` / `Connection.cs`），同样标注为非官方实现。

---

## 四、来源与合规

- 原始程序集 **不包含在本仓库内**；`skeleton/` 由元数据重新生成，`reconstructed/` 与 `src/` 来自反编译器输出。
- 本仓库作品按 **GPL-3.0** 发布。
- 上游官方 **客户端 SDK**（`massarolucien/Mass.Sdk`，MIT）的原文副本放在过程仓库的 `refs/` 下，按其 MIT 许可保留署名。
