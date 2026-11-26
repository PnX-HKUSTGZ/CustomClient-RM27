# RoboMaster Custom Client (RM2027)

Event-driven, layered C# custom operator terminal built on **Godot 4.7.2 .NET** and **.NET 8**.
All code, comments, identifiers, filenames, and directories use English; only user-facing UI strings remain in Chinese.

## Architecture Overview

Strict unidirectional dependencies across three .NET assemblies:

```text
RmClient.App        (Godot 4.7.2 .NET — scenes, HUD, 11 modal panels, input, rm_video bridge)
   ├──▶ RmClient.Transport (Pure C# — DemoTelemetrySource, PeripheralHub, reserved MQTT/RM2027/MultiClient)
   └──▶ RmClient.Core      (Pure C# — zero Godot/network dependencies: EventBus, events, state, commands, settings)
```

See [docs/architecture.md](docs/architecture.md) for detailed module boundaries, event contracts, and reserved extension points.

## Prerequisites

- **.NET 8 SDK** (verified with `8.0.427`)
- **Godot 4.7.2 .NET** (`4.7.2.stable.mono`)

## Build & Test

```powershell
# Build all projects and run pure C# unit tests (Core + Transport)
dotnet test RmClient.sln

# Import Godot assets and run headless C# SceneTree self-test
$godot = Join-Path (Split-Path $env:GODOT_PATH -Parent) "Godot_v4.7.2-stable_mono_win64_console.exe"
& $godot --headless --path "src\RmClient.App" --import
& $godot --headless --path "src\RmClient.App" -- --static-demo --self-test

# Launch the interactive client window
& $godot --path "src\RmClient.App"
```

> Note: On Windows with WinGet installations, prefer the direct `Godot_v4.7.2-stable_mono_win64_console.exe` binary over the WinGet shim when running headless `.NET` commands.

## Controls

| Shortcut | Action |
|---|---|
| `Tab` (hold) | Scoreboard panel (restores previous panel on release) |
| `` ` `` / `~` (hold) | Modules & damage summary panel |
| `F12` (hold) | Shortcut help panel |
| `P` | Settings panel (demo login, FPS/video, hardware, UI toggles) |
| `M` | Tactical map panel (`A` attack / `B` defend / `I` alert, left-click mark, right-click undo) |
| `O` / `I` | 17 mm / 42 mm ammo supply panel with two-step confirmation |
| `H` | Remote exchange panel with two-step confirmation |
| `F` / `K` | Rune / Hero deployment local demo panels |
| `Esc` | Close active panel |
| `F5` / `F6` / `F8` | Demo-only: reset state / pause or resume clock / settlement panel |

## Image Transmission (`addons/rm_video`)

Integrates the native `rm_video` GDExtension (`RmVideoSession` + `RmVideoSurface`, UDP port `3334`, 8-byte big-endian header + HEVC) through `scripts/Video/VideoLink.cs` and `scripts/Ui/Components/VideoSurface.cs`.
CLI overrides after `--`: `--no-video`, `--video-port=N`, `--video-bind=IP`, `--video-source=IP`, `--no-ui-motion`, `--static-demo`.

## Reserved Extension Directories

The following folders are scaffolded for upcoming phases and intentionally contain no business implementation yet:

- `src/RmClient.Transport/Mqtt/` — MQTTnet client & reconnect lifecycle
- `src/RmClient.Transport/Protocol/Rm2027/` — RM2027 V1.0.0 Protobuf codecs & event mappers (using `proto/*.proto`)
- `src/RmClient.Transport/MultiClient/` — Multi-station session identity & sync
- `src/RmClient.Core/Profiles/` — Vehicle profiles & configurable HUD slot layouts
- `src/RmClient.App/scripts/Ui/Widgets/` — Vehicle-specific modular HUD widgets
- `src/RmClient.App/scripts/Ui/CustomProtocol/` — Official `0x0310`/`0x0311` custom graphic UI renderer
