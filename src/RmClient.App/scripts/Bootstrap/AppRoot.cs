using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Settings;
using RmClient.App.Theme;
using RmClient.App.Ui.Components;
using RmClient.App.Ui.Hud;
using RmClient.App.Ui.Panels;
using RmClient.App.Video;
using RmClient.Core.Commands;
using RmClient.Core.Events;
using RmClient.Core.Events.Commands;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Settings;
using RmClient.Core.State;
using RmClient.Transport.Abstractions;
using RmClient.Transport.Demo;
using RmClient.Transport.Peripherals;

namespace RmClient.App.Bootstrap;

/// <summary>
/// Composition root mounted on scenes/main.tscn.
/// Wires EventBus, TerminalStore, TerminalCommands, DemoTelemetrySource, VideoLink,
/// PeripheralHub, BattleHud, and PanelHost, and owns global keyboard routing.
/// </summary>
public partial class AppRoot : Control
{
    public IEventBus Bus { get; private set; } = null!;
    public TerminalStore Store { get; private set; } = null!;
    public TerminalCommands Commands { get; private set; } = null!;
    public UserSettingsService Settings { get; private set; } = null!;
    public ITelemetrySource Telemetry { get; private set; } = null!;
    public VideoLink VideoStream { get; private set; } = null!;
    public PeripheralHub Peripherals { get; private set; } = null!;

    private BattleHud _hud = null!;
    private PanelHost _panels = null!;
    private VideoSurface _videoSurface = null!;
    private CustomUiLayer _customUiLayer = null!;
    private Label _toast = null!;
    private Timer _toastTimer = null!;

    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<string> _heldPanels = new();
    private string _persistentPanel = string.Empty;
    private bool _paused;

    private VideoStreamConfig? _videoConfig;
    private bool _videoRestartPending;
    private string _videoText = "图传 · 未启用";
    private bool _videoLive;
    private string _videoError = string.Empty;

    public override void _Ready()
    {
        _hud = GetNode<BattleHud>("%BattleHud");
        _panels = GetNode<PanelHost>("%PanelHost");
        _videoSurface = GetNode<VideoSurface>("%VideoSurface");
        _customUiLayer = GetNode<CustomUiLayer>("%CustomUiLayer");
        _toast = GetNode<Label>("%Toast");

        Bus = new EventBus();
        Store = new TerminalStore(Bus);
        Commands = new TerminalCommands(Store);
        Settings = new UserSettingsService(Bus, new GodotSettingsRepository());
        Settings.Load();
        Telemetry = new DemoTelemetrySource(Store);

        _toastTimer = new Timer
        {
            OneShot = true,
            IgnoreTimeScale = true,
            WaitTime = 3.8,
        };
        _toastTimer.Timeout += FadeFeedback;
        AddChild(_toastTimer);

        _hud.Bind(Store, Settings);
        UiMotion.AttachButtons(_hud);

        _panels.Show();
        _panels.Bind(Store, Commands, Settings);
        _panels.ClearRequested += _customUiLayer.ClearWidgets;
        _hud.PanelRequested += TogglePanel;

        _subscriptions.Add(Bus.Subscribe<CommandFeedbackEvent>(e => ShowFeedback(e.Message)));
        _subscriptions.Add(Bus.Subscribe<SettingsChangedEvent>(_ => ApplySettings()));
        _subscriptions.Add(Bus.Subscribe<DemoStateResetEvent>(_ => ProjectVideo()));

        SetupVideo();
        SetupPeripherals();
        ApplySettings();

        if (HasCmdlineArg("--self-test"))
        {
            Callable.From(() => AppSelfTest.RunAndQuit(this)).CallDeferred();
            return;
        }

        if (!HasCmdlineArg("--static-demo"))
        {
            Telemetry.Start();
        }

        var window = GetWindow();
        if (window is not null)
        {
            window.Title = "RoboMaster · 自定义选手端";
        }
    }

    public override void _Process(double delta)
    {
        Telemetry?.Tick(delta);
    }

    public override void _ExitTree()
    {
        Telemetry?.Stop();
        Peripherals?.Dispose();
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || keyEvent.Echo)
        {
            return;
        }

        var key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
        var heldId = key switch
        {
            Key.Tab => PanelHost.Scoreboard,
            Key.Quoteleft => PanelHost.Modules,
            Key.F12 => PanelHost.Help,
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(heldId))
        {
            SetHeldPanel(heldId, keyEvent.Pressed);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!keyEvent.Pressed)
        {
            return;
        }

        var requested = string.Empty;
        var handled = false;
        switch (key)
        {
            case Key.P:
                requested = PanelHost.Settings;
                break;
            case Key.M:
                requested = PanelHost.Map;
                break;
            case Key.O:
                requested = PanelHost.Supply17;
                break;
            case Key.I:
                if (_panels.CurrentPanel == PanelHost.Map)
                {
                    return;
                }
                requested = PanelHost.Supply42;
                break;
            case Key.H:
                requested = PanelHost.Exchange;
                break;
            case Key.F:
                requested = PanelHost.Rune;
                break;
            case Key.K:
                requested = PanelHost.Deployment;
                break;
            case Key.Escape:
                _persistentPanel = string.Empty;
                _heldPanels.Clear();
                _panels.CloseAll();
                handled = true;
                break;
            case Key.F5:
                Telemetry.Stop();
                Commands.ResetDemo();
                if (!HasCmdlineArg("--static-demo"))
                {
                    Telemetry.Start();
                }
                handled = true;
                break;
            case Key.F6:
                _paused = !_paused;
                Telemetry.SetPaused(_paused);
                ShowFeedback(_paused ? "演示时间已暂停" : "演示时间已继续");
                handled = true;
                break;
            case Key.F8:
                requested = PanelHost.Settlement;
                break;
        }

        if (!string.IsNullOrEmpty(requested))
        {
            TogglePanel(requested);
            handled = true;
        }

        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetHeldPanel(string id, bool pressed)
    {
        if (pressed)
        {
            if (_heldPanels.Count == 0)
            {
                _persistentPanel = _panels.CurrentPanel;
            }
            if (!_heldPanels.Contains(id))
            {
                _heldPanels.Add(id);
            }
            _panels.ShowPanel(id);
        }
        else
        {
            _heldPanels.Remove(id);
            if (_heldPanels.Count > 0)
            {
                _panels.ShowPanel(_heldPanels[^1]);
            }
            else if (!string.IsNullOrEmpty(_persistentPanel))
            {
                _panels.ShowPanel(_persistentPanel);
            }
            else
            {
                _panels.CloseAll();
            }
        }
    }

    private void TogglePanel(string id)
    {
        _heldPanels.Clear();
        _persistentPanel = _panels.CurrentPanel == id ? string.Empty : id;
        _panels.TogglePanel(id);
    }

    private void ApplySettings()
    {
        var cfg = Settings.Current;
        _customUiLayer.Visible = cfg.ShowCustomUi;
        _videoSurface.SetReversed(cfg.ReverseVideo);
        Engine.MaxFps = cfg.FpsLimit;
        var targetMode = cfg.Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.GetName() != "headless" && DisplayServer.WindowGetMode() != targetMode)
        {
            DisplayServer.WindowSetMode(targetMode);
        }
        if (AudioServer.BusCount > 0)
        {
            AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(MathF.Max(cfg.Volume / 100f, 0.001f)));
        }
        SyncVideo();
    }

    private void SetupPeripherals()
    {
        Peripherals = new PeripheralHub(Bus, new GodotMainThreadDispatcher());
        _subscriptions.Add(Bus.Subscribe<PeripheralConnectionChangedEvent>(e =>
            Store.AppendSystemEvent($"外设 {e.PeripheralId} {(e.Connected ? "已连接" : "已断开")}")));
        _subscriptions.Add(Bus.Subscribe<PeripheralFaultEvent>(e =>
            Store.AppendSystemEvent($"外设 {e.PeripheralId}：{e.Message}")));
        _subscriptions.Add(Bus.Subscribe<PeripheralInputReceivedEvent>(OnPeripheralInput));
        // Extension point: register concrete IPeripheralSource adapters here.
        Peripherals.StartAll();
    }

    /// <summary>
    /// Extension point: map normalized peripheral inputs to TerminalCommands.
    /// Unmapped channels are ignored on purpose; never forward raw device data to UI.
    /// </summary>
    private void OnPeripheralInput(PeripheralInputReceivedEvent _)
    {
    }

    private void SetupVideo()
    {
        VideoStream = new VideoLink();
        AddChild(VideoStream);
        VideoStream.StatusChanged += OnVideoStatus;

        if (VideoLink.IsAvailable() && DisplayServer.GetName() != "headless")
        {
            var nativeSurface = VideoStream.CreateSurface();
            if (nativeSurface is not null)
            {
                _videoSurface.AttachNativeSurface(nativeSurface);
            }
        }
        else if (!VideoLink.IsAvailable())
        {
            SetVideoText("图传 · 视频组件未加载", false);
        }
    }

    private bool IsVideoAllowed()
    {
        return Settings.Current.VideoEnabled
               && VideoLink.IsAvailable()
               && !HasCmdlineArg("--no-video")
               && DisplayServer.GetName() != "headless";
    }

    private VideoStreamConfig GetWantedVideoConfig()
    {
        var cfg = Settings.Current.ToVideoConfig();
        var port = cfg.Port;
        var bind = cfg.BindAddress;
        var source = cfg.SourceAddress;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--video-port=", StringComparison.Ordinal)
                && int.TryParse(arg["--video-port=".Length..], out var parsedPort))
            {
                port = parsedPort;
            }
            else if (arg.StartsWith("--video-bind=", StringComparison.Ordinal))
            {
                bind = arg["--video-bind=".Length..];
            }
            else if (arg.StartsWith("--video-source=", StringComparison.Ordinal))
            {
                source = arg["--video-source=".Length..];
            }
        }
        return cfg with { Port = port, BindAddress = bind, SourceAddress = source };
    }

    private void SyncVideo()
    {
        if (VideoStream is null)
        {
            return;
        }
        if (!IsVideoAllowed())
        {
            _videoRestartPending = false;
            if (VideoStream.IsRunning)
            {
                VideoStream.Stop();
            }
            else if (!VideoStream.IsStopping && VideoLink.IsAvailable())
            {
                SetVideoText("图传 · 未启用", false);
            }
            return;
        }

        var wanted = GetWantedVideoConfig();
        if (VideoStream.IsRunning)
        {
            if (wanted == _videoConfig)
            {
                return;
            }
            _videoRestartPending = true;
            VideoStream.Stop();
            return;
        }
        if (VideoStream.IsStopping)
        {
            _videoRestartPending = true;
            return;
        }

        _videoRestartPending = false;
        _videoConfig = wanted;
        if (!VideoStream.Start(wanted))
        {
            ShowFeedback($"图传启动失败：{VideoStream.CurrentStatus.Error}");
        }
    }

    private void OnVideoStatus(VideoLinkStatus status)
    {
        var text = status.Live
            ? $"图传 · {status.Width}×{status.Height} · {status.Fps:F0} fps · {(string.IsNullOrEmpty(status.Mode) ? status.Decoder : $"{status.Decoder}/{status.Mode}")}"
            : $"图传 · {status.Label}";

        if (status.Live != _videoLive)
        {
            var started = $"开始显示 {status.Width}×{status.Height} · {status.Decoder} 解码";
            Store.AppendSystemEvent($"图传：{(status.Live ? started : "信号中断，等待恢复")}");
        }
        if (!string.IsNullOrEmpty(status.Error) && status.Error != _videoError)
        {
            var trimmed = status.Error.Length > 80 ? status.Error[..80] : status.Error;
            Store.AppendSystemEvent($"图传错误：{trimmed}");
        }

        _videoError = status.Error;
        _videoSurface.SetLive(status.Live);
        SetVideoText(text, status.Live, status.Error);

        if (_videoRestartPending && !VideoStream.IsRunning && !VideoStream.IsStopping)
        {
            SyncVideo();
        }
    }

    private void SetVideoText(string text, bool live, string error = "")
    {
        _videoText = text;
        _videoLive = live;
        Store.SetVideoStatus(text, live, error);
    }

    private void ProjectVideo()
    {
        Store.SetVideoStatus(_videoText, _videoLive, _videoError);
    }

    private void ShowFeedback(string message)
    {
        UiMotion.Cancel(_toast, "toast_exit");
        _toast.Text = message;
        UiMotion.Present(_toast);
        _toastTimer.Start();
    }

    private void FadeFeedback()
    {
        var tween = UiMotion.Animate(_toast, new NodePath("modulate:a"), Variant.From(0f), 0.15f, "toast_exit");
        if (tween is not null)
        {
            tween.Finished += FinishFeedback;
        }
        else
        {
            FinishFeedback();
        }
    }

    private void FinishFeedback()
    {
        _toast.Text = string.Empty;
        UiMotion.Dismiss(_toast);
    }

    private static bool HasCmdlineArg(string flag)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == flag)
            {
                return true;
            }
        }
        return false;
    }
}
