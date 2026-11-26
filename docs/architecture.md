# Architecture & Module Boundaries

## Layered Solution Structure

```text
RmClient.sln
├── src/RmClient.Core/          Zero Godot or network dependencies
│   ├── Events/                 IEventBus, EventBus, single-purpose domain events
│   │   ├── Match/              MatchClockUpdatedEvent, BaseHealthUpdatedEvent, TeamScoreUpdatedEvent, CreditsUpdatedEvent, DemoStateResetEvent
│   │   ├── Robot/              ActiveRobotChangedEvent, RobotHealthUpdatedEvent, RobotAmmoUpdatedEvent, RobotHeatPowerUpdatedEvent, RobotEnergyUpdatedEvent, RobotPositionUpdatedEvent
│   │   ├── Tactical/           MapMarkerSubmittedEvent, RobotMessageUpdatedEvent, SystemEventAppendedEvent
│   │   ├── Runtime/            ConnectionStatusChangedEvent, ModuleStatusUpdatedEvent, VideoStatusUpdatedEvent, SettingsChangedEvent, Peripheral* events
│   │   └── Commands/           CommandFeedbackEvent
│   ├── State/                  DomainTypes, MatchState, TeamState, RobotState, TerminalStore
│   ├── Commands/               TerminalCommands (atomic local validation & execution)
│   ├── Settings/               UserSettingsData, ISettingsRepository, UserSettingsService
│   └── Profiles/               [Reserved] Vehicle profiles & HUD slot configs
│
├── src/RmClient.Transport/     Zero Godot dependencies
│   ├── Abstractions/           ITelemetrySource, IMainThreadDispatcher, ImmediateDispatcher
│   ├── Demo/                   DemoTelemetrySource (deterministic 1Hz event publisher)
│   ├── Peripherals/            IPeripheralSource, PeripheralHub
│   ├── Mqtt/                   [Reserved] MQTTnet client
│   ├── Protocol/Rm2027/        [Reserved] Protobuf codecs & protocol-to-event mappers
│   └── MultiClient/            [Reserved] Multi-client sync
│
└── src/RmClient.App/           Godot 4.7.2 .NET project
    ├── addons/rm_video/        Native HEVC UDP GDExtension (unchanged binary + LGPL materials)
    ├── assets/                 Portraits, reference images, official_theme.tres
    ├── scenes/                 main.tscn, hud/battle_hud.tscn, panels/*.tscn
    └── scripts/
        ├── Bootstrap/          AppRoot, GodotMainThreadDispatcher, AppSelfTest
        ├── Settings/           GodotSettingsRepository (ConfigFile)
        ├── Video/              VideoLink (GDExtension wrapper + HEVC parameter cache)
        ├── Theme/              UiKit, UiMotion
        └── Ui/
            ├── Components/     VideoSurface, HudPlate, BeveledBar, OperatorCrosshair, ArenaMap, MapMarkerIcon, RobotCard, RobotPortrait
            ├── Hud/            BattleHud, CustomUiLayer
            ├── Panels/         PanelHost, TerminalPanel, and 9 concrete panel scripts (11 scenes)
            ├── Widgets/        [Reserved] Vehicle-specific HUD slot widgets
            └── CustomProtocol/ [Reserved] Official 0x0310/0x0311 UI renderer
```

## Design Rules

1. **Single-purpose independent events**: Each event represents one distinct fact (e.g., clock tick, robot ammo update, module status change, command feedback). Subscribers register only for the event types they care about, avoiding full-HUD redraws on unrelated state mutations.
2. **Thread safety boundary**: Background transport, video, and peripheral threads never touch the Godot SceneTree. They marshal callbacks through `IMainThreadDispatcher` (`GodotMainThreadDispatcher` via `CallDeferred`) before publishing events onto `IEventBus`.
3. **UI isolation**: `RmClient.App` UI scripts never reference MQTT, Protobuf, or native decoder handles. `VideoLink` encapsulates `RmVideoSession`/`RmVideoSurface`; `TerminalCommands` encapsulates rule validation.
4. **Hidden panel laziness**: Inactive `TerminalPanel` instances mark themselves dirty upon receiving subscribed events and refresh only when made visible.
5. **Naming convention**: All directories, filenames, namespaces, identifiers, and comments are written in English; Chinese is restricted to user-facing UI strings.
