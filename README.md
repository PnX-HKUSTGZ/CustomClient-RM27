# CustomClient RM27

> RM27赛季基于 Godot 4.7.2 .NET与.NET 8的自定义选手端

## 项目简介

本仓库是 RM27 赛季的自定义选手端工程，最终目标是完全替代官方选手端，并支持多自定义客户端协同、按车种定制 HUD 以及自定义外设扩展。

详细架构设计与事件清单见 [docs/architecture.md](docs/architecture.md)。


## 运行环境

| 项目 | 说明 |
| --- | --- |
| 目标运行环境 | Windows 11 x64（Godot 4.7.2 .NET，`GL Compatibility` 渲染器） |
| 核心框架 | .NET 8 SDK（已验证 `8.0.427`）+ Godot 4.7.2 .NET（`4.7.2.stable.mono`） |
| 测试框架 | xUnit（纯 C# 单元测试）+ Godot C# SceneTree 无头自检（`--self-test`） |
| 原生图传扩展 | `addons/rm_video`（来自 `PnX-HKUSTGZ/rm_udp_streamer`，UDP 3334 HEVC 硬解/软解） |

## 快速开始

```powershell
# 一键全量验证（dotnet build → dotnet test → Godot 资产导入 → C# SceneTree 自检）
.\tools\verify.ps1

# 仅编译并运行纯 C# 单元测试（无需安装 Godot）
dotnet test RmClient.sln

# 手动运行 Godot 无头自检（推荐直接调用安装目录的 *_console.exe，避免 WinGet shim 挂起）
$godot = Join-Path (Split-Path $env:GODOT_PATH -Parent) "Godot_v4.7.2-stable_mono_win64_console.exe"
& $godot --headless --path "src\RmClient.App" --import
& $godot --headless --path "src\RmClient.App" -- --static-demo --self-test

# 启动交互式客户端窗口
& $godot --path "src\RmClient.App"

# 常用启动参数（放在 -- 之后）
# --static-demo          固定演示时间，不推进倒计时
# --no-video             禁用 UDP 图传接收
# --no-ui-motion         关闭轻量界面过渡动效
# --video-port=3334      临时覆盖图传监听端口（支持 --video-bind=IP / --video-source=IP）
```

## 核心架构与目录结构

三层 `.NET` 程序集保持严格单向依赖：`RmClient.App` → `RmClient.Transport` → `RmClient.Core`。`Core` 与 `Transport` 零 `Godot` 依赖，UI 层严禁直接引用 MQTT/Protobuf 或原生图传类。

```text
CustomClient-RM27/
├── RmClient.sln                      # .NET 8 解决方案入口
├── Directory.Build.props             # 统一 C# 语言版本、Nullable 与编译选项
├── .github/workflows/ci.yml          # GitHub Actions CI（复用 tools/verify.ps1）
├── docs/
│   └── architecture.md               # 完整架构设计、Mermaid 架构/时序图与事件清单
├── proto/                            # RM2027 V1.0.0 .proto 协议定义（与后端同步）
├── tools/
│   ├── verify.ps1                    # 本地与 CI 共用的一键验证脚本
│   └── send_hevc.py                  # 本机 HEVC UDP 图传回环测试发包脚本
├── src/
│   ├── RmClient.Core/                # 纯 C# 领域层：EventBus、单一职责事件、TerminalStore、TerminalCommands、UserSettings
│   │   └── Profiles/                 # [预留] 车种配置与 HUD 槽位布局
│   ├── RmClient.Transport/           # 纯 C# 传输层：DemoTelemetrySource、PeripheralHub、主线程派发抽象
│   │   ├── Mqtt/                     # [预留] MQTTnet 客户端与重连管理
│   │   ├── Protocol/Rm2027/          # [预留] RM2027 Protobuf 编解码与事件映射
│   │   └── MultiClient/              # [预留] 多自定义客户端协同
│   └── RmClient.App/                 # Godot 4.7.2 .NET 工程（场景、HUD、11 个面板、主题、图传桥接）
│       ├── addons/rm_video/          # 原生图传 GDExtension（rm_video.dll / librm_video.so）
│       ├── assets/                   # 图标、头像、参考贴图与主题资源
│       ├── scenes/                   # main.tscn、hud/battle_hud.tscn、panels/*.tscn
│       └── scripts/
│           ├── Bootstrap/            # AppRoot、GodotMainThreadDispatcher、AppSelfTest
│           ├── Video/                # VideoLink（封装 rm_video 与 HEVC 参数缓存）
│           ├── Settings/             # GodotSettingsRepository（ConfigFile 持久化）
│           ├── Theme/                # UiKit、UiMotion
│           └── Ui/                   # BattleHud、8 个组件、PanelHost、11 个面板及 Widgets/CustomProtocol 预留
└── tests/
    ├── RmClient.Core.Tests/          # Core 单元测试（EventBus、Store、Commands、Settings）
    ├── RmClient.Transport.Tests/     # Transport 单元测试（DemoTelemetrySource、PeripheralHub）
    └── GodotIntegration/             # Godot C# SceneTree 集成自检说明
```

## 操作快捷键

| 快捷键 | 行为 |
| --- | --- |
| `Tab`（按住） | 双方机器人状态记分板（松开恢复之前面板） |
| `` ` `` / `~`（按住） | 模块状态与伤害统计面板 |
| `F12`（按住） | 快捷键帮助面板 |
| `P` | 设置面板（演示登录、帧率/图传、硬件、UI 开关） |
| `M` | 战术大地图（`A` 进攻 / `B` 防守 / `I` 警戒，左键标记，右键撤回） |
| `O` / `I` | 17 mm / 42 mm 弹丸补给面板（二次确认） |
| `H` | 远程兑换面板（二次确认） |
| `F` / `K` | 能量机关 / 英雄部署本地演示面板 |
| `Esc` | 关闭当前面板 |
| `F5` / `F6` / `F8` | 演示专用：重置状态 / 暂停或继续计时 / 查看结算面板 |

## 文档索引

* [docs/architecture.md](docs/architecture.md)：项目分层架构、单一职责事件清单、图传与面板生命周期说明
* [tests/GodotIntegration/README.md](tests/GodotIntegration/README.md)：Godot C# 无头与可见窗口自检命令及覆盖项
* [src/RmClient.App/addons/rm_video/SOURCE.md](src/RmClient.App/addons/rm_video/SOURCE.md)：原生图传扩展来源与 LGPL 说明

## 命名与开发约定

* 仓库内除面向操作手的 UI 显示文案与项目文档外，所有目录名、文件名、代码、注释与标识符统一使用英文。
* 所有状态变更通过 `IEventBus` 发布单一含义的独立事件；后台线程回调必须经 `IMainThreadDispatcher` 切回主线程。

## 许可证
LGPL-2.1-or-later
