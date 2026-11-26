# Godot C# Integration Verification

Integration checks run directly inside Godot 4.7.2 .NET via `RmClient.App.Bootstrap.AppSelfTest`,
requiring zero GDScript files.

## Commands

```powershell
# Run full local verification (dotnet build + dotnet test + Godot import + self-test)
.\tools\verify.ps1

$godot = Join-Path (Split-Path $env:GODOT_PATH -Parent) "Godot_v4.7.2-stable_mono_win64_console.exe"

# Headless SceneTree self-test (also wired in .github/workflows/ci.yml)
& $godot --headless --path "src\RmClient.App" -- --static-demo --self-test

# Visible OpenGL window self-test (also exercises RmVideoSurface attachment)
& $godot --path "src\RmClient.App" --resolution 1536x864 -- --static-demo --no-video --self-test
```

## Coverage

- Core service bootstrap and initial HUD projections
- Mutual exclusivity of all 11 panels via `PanelHost`
- Toggle shortcuts (`P`, `M`, `O`, `I`, `H`, `F`, `K`, `F8`, `Esc`) and held shortcuts (`Tab`, `~`, `F12`) restoring prior panels
- Two-step confirmation flow on 17 mm / 42 mm supply panels and event-driven HUD updates
- Map panel marker addition, undo, and `I` shortcut isolation while map is active
- Exported C# properties on scenes (`Caliber`, `ActionName`)
- `SettingsChangedEvent` visibility projection onto HUD and `CustomUiLayer`
- `F5` demo reset and native `rm_video` GDExtension class registration check
