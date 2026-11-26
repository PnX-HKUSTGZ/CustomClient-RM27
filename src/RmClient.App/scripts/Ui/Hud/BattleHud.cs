using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.App.Ui.Components;
using RmClient.App.Ui.Panels;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Events.Tactical;
using RmClient.Core.Settings;
using RmClient.Core.State;

namespace RmClient.App.Ui.Hud;

/// <summary>
/// Main combat HUD controller driven by independent single-purpose domain events from IEventBus.
/// </summary>
public partial class BattleHud : Control
{
    public event Action<string>? PanelRequested;

    private static readonly Dictionary<string, string> ModuleKeyMap = new(StringComparer.Ordinal)
    {
        ["机器人"] = "referee",
        ["遥控器"] = "remote_controller",
        ["自定义控制器"] = "custom_terminal",
        ["图传"] = "video",
        ["RFID识别"] = "rfid",
        ["模块"] = "referee",
        ["UWB"] = "uwb",
        ["Marker"] = "marker",
        ["底盘"] = "chassis",
        ["云台"] = "gimbal",
        ["17mm电源"] = "shooter",
    };

    private TerminalStore? _store;
    private UserSettingsService? _settings;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<RobotCard> _cards = new();
    private readonly Dictionary<string, Label> _moduleIndicators = new(StringComparer.Ordinal);
    private bool _indicatorsBuilt;

    public ArenaMap Minimap { get; private set; } = null!;
    public OperatorCrosshair Crosshair { get; private set; } = null!;
    public RobotPortrait Portrait { get; private set; } = null!;

    private BeveledBar _redBaseBar = null!;
    private BeveledBar _blueBaseBar = null!;
    private Label _redBaseHealth = null!;
    private Label _blueBaseHealth = null!;
    private Label _redOutpost = null!;
    private Label _blueOutpost = null!;
    private Label _aliveRed = null!;
    private Label _aliveBlue = null!;
    private Label _redScore = null!;
    private Label _blueScore = null!;
    private Label _round = null!;
    private Label _time = null!;
    private Label _credits = null!;
    private Label _demoBadge = null!;
    private Label _videoStatus = null!;
    private Label _localHealth = null!;
    private BeveledBar _localHealthBar = null!;
    private Label _activeId = null!;
    private Label _ammo = null!;
    private Label _muzzleSpeed = null!;
    private BeveledBar _energyBar = null!;
    private BeveledBar _bufferBar = null!;
    private Label _bufferText = null!;
    private Label _heatText = null!;
    private Label _combatState = null!;
    private Label _systemEvents = null!;
    private Label _robotMessages = null!;

    public override void _Ready()
    {
        Minimap = GetNode<ArenaMap>("%MiniMap");
        Crosshair = GetNode<OperatorCrosshair>("%Crosshair");
        Portrait = GetNode<RobotPortrait>("LocalPortrait");

        _redBaseBar = GetNode<BeveledBar>("%RedBaseBar");
        _blueBaseBar = GetNode<BeveledBar>("%BlueBaseBar");
        _redBaseHealth = GetNode<Label>("%RedBaseHealth");
        _blueBaseHealth = GetNode<Label>("%BlueBaseHealth");
        _redOutpost = GetNode<Label>("%RedOutpost");
        _blueOutpost = GetNode<Label>("%BlueOutpost");
        _aliveRed = GetNode<Label>("%AliveRed");
        _aliveBlue = GetNode<Label>("%AliveBlue");
        _redScore = GetNode<Label>("%RedScore");
        _blueScore = GetNode<Label>("%BlueScore");
        _round = GetNode<Label>("%Round");
        _time = GetNode<Label>("%Time");
        _credits = GetNode<Label>("%Credits");
        _demoBadge = GetNode<Label>("%DemoBadge");
        _videoStatus = GetNode<Label>("%VideoStatus");
        _localHealth = GetNode<Label>("%LocalHealth");
        _localHealthBar = GetNode<BeveledBar>("%LocalHealthBar");
        _activeId = GetNode<Label>("%ActiveId");
        _ammo = GetNode<Label>("%Ammo");
        _muzzleSpeed = GetNode<Label>("%MuzzleSpeed");
        _energyBar = GetNode<BeveledBar>("%EnergyBar");
        _bufferBar = GetNode<BeveledBar>("%BufferBar");
        _bufferText = GetNode<Label>("%BufferText");
        _heatText = GetNode<Label>("%HeatText");
        _combatState = GetNode<Label>("%CombatState");
        _systemEvents = GetNode<Label>("%SystemEvents");
        _robotMessages = GetNode<Label>("%RobotMessages");

        GetNode<Button>("%RunePrompt").Pressed += () => PanelRequested?.Invoke(PanelHost.Rune);
        GetNode<Button>("%ExchangePrompt").Pressed += () => PanelRequested?.Invoke(PanelHost.Exchange);

        foreach (var child in GetChildren())
        {
            if (child is RobotCard card)
            {
                _cards.Add(card);
                var capturedCard = card;
                card.Pressed += () => _store?.SelectRobot(capturedCard.RobotId, capturedCard.Side);
            }
        }

        GetNode<Control>("%ModuleRow1").Hide();
        GetNode<Control>("%ModuleRow2").Hide();
        BuildModuleIndicators();
    }

    public override void _ExitTree()
    {
        ClearSubscriptions();
    }

    public void Bind(TerminalStore store, UserSettingsService settings)
    {
        ClearSubscriptions();
        _store = store;
        _settings = settings;
        Minimap.Bind(store);

        var bus = store.Bus;
        _subscriptions.Add(bus.Subscribe<MatchClockUpdatedEvent>(OnClockUpdated));
        _subscriptions.Add(bus.Subscribe<TeamScoreUpdatedEvent>(OnTeamScoreUpdated));
        _subscriptions.Add(bus.Subscribe<BaseHealthUpdatedEvent>(OnBaseHealthUpdated));
        _subscriptions.Add(bus.Subscribe<CreditsUpdatedEvent>(OnCreditsUpdated));
        _subscriptions.Add(bus.Subscribe<ActiveRobotChangedEvent>(OnActiveRobotChanged));
        _subscriptions.Add(bus.Subscribe<RobotHealthUpdatedEvent>(OnRobotHealthUpdated));
        _subscriptions.Add(bus.Subscribe<RobotAmmoUpdatedEvent>(OnRobotAmmoUpdated));
        _subscriptions.Add(bus.Subscribe<RobotHeatPowerUpdatedEvent>(OnRobotHeatPowerUpdated));
        _subscriptions.Add(bus.Subscribe<RobotEnergyUpdatedEvent>(OnRobotEnergyUpdated));
        _subscriptions.Add(bus.Subscribe<SystemEventAppendedEvent>(_ => RefreshSystemEvents()));
        _subscriptions.Add(bus.Subscribe<RobotMessageUpdatedEvent>(e => _robotMessages.Text = e.Message));
        _subscriptions.Add(bus.Subscribe<ModuleStatusUpdatedEvent>(OnModuleStatusUpdated));
        _subscriptions.Add(bus.Subscribe<VideoStatusUpdatedEvent>(OnVideoStatusUpdated));
        _subscriptions.Add(bus.Subscribe<ConnectionStatusChangedEvent>(e => _demoBadge.Text = $"{e.Label}    F12 帮助"));
        _subscriptions.Add(bus.Subscribe<DemoStateResetEvent>(_ => RefreshAll(snapBars: true, pulsePortrait: false)));
        _subscriptions.Add(bus.Subscribe<SettingsChangedEvent>(_ => RefreshSettings()));

        RefreshAll(snapBars: true, pulsePortrait: false);
        RefreshSettings();
    }

    private void ClearSubscriptions()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }

    private void BuildModuleIndicators()
    {
        if (_indicatorsBuilt)
        {
            return;
        }
        _indicatorsBuilt = true;
        string[][] rows =
        {
            new[] { "机器人", "遥控器", "自定义控制器", "图传", "RFID识别" },
            new[] { "模块", "UWB", "Marker", "底盘", "云台", "17mm电源" },
        };

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var x = 43f;
            foreach (var title in rows[rowIndex])
            {
                var width = 38f + title.Length * 16f;
                var y = -68f + rowIndex * 33f;
                var indicator = UiKit.CreateLabel(this, "▰", new Rect2(x, y, 19f, 28f), 20, UiKit.Green);
                var caption = UiKit.CreateLabel(this, title, new Rect2(x + 20f, y, width - 20f, 28f), 16);
                foreach (var node in new[] { indicator, caption })
                {
                    node.AnchorTop = 1f;
                    node.AnchorBottom = 1f;
                }
                _moduleIndicators[title] = indicator;
                x += width + 10f;
            }
        }
    }

    private void RefreshAll(bool snapBars, bool pulsePortrait)
    {
        if (_store is null)
        {
            return;
        }
        var state = _store.State;
        OnClockUpdated(new MatchClockUpdatedEvent(state.RoundIndex, state.RemainingSeconds));
        OnTeamScoreUpdated(new TeamScoreUpdatedEvent(TeamSide.Red, state.Red.Score));
        OnTeamScoreUpdated(new TeamScoreUpdatedEvent(TeamSide.Blue, state.Blue.Score));
        OnBaseHealthUpdated(new BaseHealthUpdatedEvent(TeamSide.Red, state.Red.BaseHealth, state.Red.BaseMaxHealth, state.Red.OutpostHealth));
        OnBaseHealthUpdated(new BaseHealthUpdatedEvent(TeamSide.Blue, state.Blue.BaseHealth, state.Blue.BaseMaxHealth, state.Blue.OutpostHealth));
        OnCreditsUpdated(new CreditsUpdatedEvent(state.Credits));
        _demoBadge.Text = $"{state.ConnectionLabel}    F12 帮助";
        var videoOnline = state.Modules.TryGetValue("video", out var vLive) && vLive;
        OnVideoStatusUpdated(new VideoStatusUpdatedEvent(state.VideoLabel, videoOnline, string.Empty));
        _robotMessages.Text = state.RobotMessage;
        RefreshAliveCounts();
        RefreshAllCards();
        RefreshActiveRobot(snapBars, pulsePortrait);
        RefreshSystemEvents();
        RefreshAllModules();

        if (snapBars)
        {
            _redBaseBar.SnapToValue();
            _blueBaseBar.SnapToValue();
        }
    }

    private void OnClockUpdated(MatchClockUpdatedEvent e)
    {
        _round.Text = $"第 {e.RoundIndex} 局 · 比赛进行中";
        _time.Text = $"{e.RemainingSeconds / 60}:{e.RemainingSeconds % 60:D2}";
        SetFontColor(_time, e.RemainingSeconds <= 10 ? UiKit.Red : UiKit.Text);
    }

    private void OnTeamScoreUpdated(TeamScoreUpdatedEvent e)
    {
        if (e.Side == TeamSide.Red)
        {
            _redScore.Text = e.Score.ToString();
        }
        else
        {
            _blueScore.Text = e.Score.ToString();
        }
    }

    private void OnBaseHealthUpdated(BaseHealthUpdatedEvent e)
    {
        var ratio = (float)e.BaseHealth / Math.Max(e.BaseMaxHealth, 1);
        if (e.Side == TeamSide.Red)
        {
            _redBaseBar.Ratio = ratio;
            _redBaseHealth.Text = e.BaseHealth.ToString();
            _redOutpost.Text = $"前哨站 {e.OutpostHealth}";
        }
        else
        {
            _blueBaseBar.Ratio = ratio;
            _blueBaseHealth.Text = e.BaseHealth.ToString();
            _blueOutpost.Text = $"前哨站 {e.OutpostHealth}";
        }
    }

    private void OnCreditsUpdated(CreditsUpdatedEvent e)
    {
        _credits.Text = $"{e.Credits}  ◉";
    }

    private void OnActiveRobotChanged(ActiveRobotChangedEvent _)
    {
        RefreshAllCards();
        RefreshActiveRobot(snapBars: true, pulsePortrait: true);
    }

    private void OnRobotHealthUpdated(RobotHealthUpdatedEvent e)
    {
        RefreshCard(e.Team, e.RobotId);
        RefreshAliveCounts();
        if (_store is not null && e.Team == _store.State.ActiveTeam && e.RobotId == _store.State.ActiveRobotId)
        {
            var robot = _store.State.GetActiveRobot();
            if (robot is not null)
            {
                UpdateActiveHealth(robot, _store.State.ActiveTeam);
            }
        }
    }

    private void OnRobotAmmoUpdated(RobotAmmoUpdatedEvent e)
    {
        RefreshCard(e.Team, e.RobotId);
        if (_store is not null && e.Team == _store.State.ActiveTeam && e.RobotId == _store.State.ActiveRobotId)
        {
            _ammo.Text = e.Ammo.ToString();
            _muzzleSpeed.Text = $"{e.MuzzleSpeed:F1}";
        }
    }

    private void OnRobotHeatPowerUpdated(RobotHeatPowerUpdatedEvent e)
    {
        if (_store is null || e.Team != _store.State.ActiveTeam || e.RobotId != _store.State.ActiveRobotId)
        {
            return;
        }
        UpdateActiveHeat(e.Heat, e.HeatLimit);
    }

    private void OnRobotEnergyUpdated(RobotEnergyUpdatedEvent e)
    {
        if (_store is null || e.Team != _store.State.ActiveTeam || e.RobotId != _store.State.ActiveRobotId)
        {
            return;
        }
        UpdateActiveEnergy(e.ChassisEnergy, e.BufferEnergy);
    }

    private void OnModuleStatusUpdated(ModuleStatusUpdatedEvent e)
    {
        foreach (var pair in _moduleIndicators)
        {
            var moduleKey = ModuleKeyMap.TryGetValue(pair.Key, out var mapped) ? mapped : pair.Key;
            if (moduleKey == e.ModuleKey)
            {
                SetFontColor(pair.Value, e.Online ? UiKit.Green : UiKit.Red);
            }
        }
    }

    private void OnVideoStatusUpdated(VideoStatusUpdatedEvent e)
    {
        _videoStatus.Text = e.Label;
        SetFontColor(_videoStatus, e.Live ? UiKit.Cyan : UiKit.Dim);
    }

    private void RefreshAliveCounts()
    {
        if (_store is null)
        {
            return;
        }
        _aliveRed.Text = $"红方存活 {_store.State.Red.AliveCount()}";
        _aliveBlue.Text = $"蓝方存活 {_store.State.Blue.AliveCount()}";
    }

    private void RefreshAllCards()
    {
        if (_store is null)
        {
            return;
        }
        var state = _store.State;
        foreach (var card in _cards)
        {
            var team = state.GetTeam(card.Side);
            var member = team?.FindRobot(card.RobotId);
            if (member is not null)
            {
                var isActive = state.ActiveRobotId == member.Id && state.ActiveTeam.ToCode() == card.Side;
                card.UpdateRobot(member, isActive);
            }
        }
    }

    private void RefreshCard(TeamSide side, int robotId)
    {
        if (_store is null)
        {
            return;
        }
        var state = _store.State;
        var sideCode = side.ToCode();
        var member = state.GetTeam(side).FindRobot(robotId);
        if (member is null)
        {
            return;
        }
        foreach (var card in _cards)
        {
            if (card.Side == sideCode && card.RobotId == robotId)
            {
                var isActive = state.ActiveRobotId == robotId && state.ActiveTeam == side;
                card.UpdateRobot(member, isActive);
            }
        }
    }

    private void RefreshActiveRobot(bool snapBars, bool pulsePortrait)
    {
        if (_store is null)
        {
            return;
        }
        var state = _store.State;
        var robot = state.GetActiveRobot();
        if (robot is null)
        {
            return;
        }
        Portrait.UpdateRobot(robot, state.ActiveTeam);
        UpdateActiveHealth(robot, state.ActiveTeam);
        _activeId.Text = robot.Id.ToString();
        _ammo.Text = robot.Ammo.ToString();
        _muzzleSpeed.Text = $"{robot.MuzzleSpeed:F1}";
        UpdateActiveEnergy(robot.ChassisEnergy, robot.BufferEnergy);
        UpdateActiveHeat(robot.Heat, robot.HeatLimit);

        if (snapBars)
        {
            _localHealthBar.SnapToValue();
            _energyBar.SnapToValue();
            _bufferBar.SnapToValue();
            Crosshair.SnapToValue();
            if (pulsePortrait)
            {
                UiMotion.Pulse(Portrait);
            }
        }
    }

    private void UpdateActiveHealth(RobotState robot, TeamSide side)
    {
        var color = side == TeamSide.Red ? UiKit.Red : UiKit.Blue;
        _localHealth.Text = $"{robot.Health} / {robot.MaxHealth}";
        _localHealthBar.Ratio = (float)robot.Health / Math.Max(robot.MaxHealth, 1);
        _localHealthBar.Tint = color;
        _combatState.Text = robot.Health > 0 ? "◇  已脱战" : "◇  等待复活";
        SetFontColor(_combatState, robot.Health > 0 ? UiKit.Green : UiKit.Red);
    }

    private void UpdateActiveEnergy(float chassisEnergy, float bufferEnergy)
    {
        _energyBar.Ratio = Math.Clamp(chassisEnergy / 100f, 0f, 1f);
        _bufferBar.Ratio = Math.Clamp(bufferEnergy / 60f, 0f, 1f);
        _bufferText.Text = $"缓冲能量 {(int)MathF.Round(bufferEnergy)} J";
    }

    private void UpdateActiveHeat(float heat, float heatLimit)
    {
        _heatText.Text = $"热量 {(int)MathF.Round(heat)} / {(int)MathF.Round(heatLimit)}";
        SetFontColor(_heatText, heat >= heatLimit * 0.75f ? UiKit.Red : UiKit.Gold);
        Crosshair.HeatRatio = heat / MathF.Max(heatLimit, 1f);
    }

    private void RefreshSystemEvents()
    {
        if (_store is null)
        {
            return;
        }
        var events = _store.State.Events;
        var start = Math.Max(0, events.Count - 4);
        var visible = new List<string>(4);
        for (var i = start; i < events.Count; i++)
        {
            visible.Add(events[i]);
        }
        _systemEvents.Text = string.Join("\n", visible);
    }

    private void RefreshAllModules()
    {
        if (_store is null)
        {
            return;
        }
        var modules = _store.State.Modules;
        foreach (var pair in _moduleIndicators)
        {
            var key = ModuleKeyMap.TryGetValue(pair.Key, out var mapped) ? mapped : pair.Key;
            var online = modules.TryGetValue(key, out var val) && val;
            SetFontColor(pair.Value, online ? UiKit.Green : UiKit.Red);
        }
    }

    private void RefreshSettings()
    {
        if (_settings is null)
        {
            return;
        }
        Minimap.Visible = _settings.Current.ShowMinimap;
        Crosshair.Visible = _settings.Current.ShowCrosshair;
    }

    private static void SetFontColor(Label label, Color color)
    {
        if (label.GetThemeColor("font_color") != color)
        {
            label.AddThemeColorOverride("font_color", color);
        }
    }
}
