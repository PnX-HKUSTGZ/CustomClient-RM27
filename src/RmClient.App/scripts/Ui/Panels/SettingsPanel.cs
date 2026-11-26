using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Settings;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class SettingsPanel : TerminalPanel
{
    public event Action? ClearRequested;

    private static readonly int[] FpsValues = { 30, 60, 120, 144, 240, 0 };
    private const double SaveDelaySeconds = 0.25;

    private Timer _saveTimer = null!;
    private bool _savePending;
    private bool _syncing;

    private OptionButton _teamChoice = null!;
    private OptionButton _robotChoice = null!;
    private readonly List<int> _robotIds = new();
    private string _lastTeam = string.Empty;
    private int _lastRobotId = -1;
    private Label _loginStatus = null!;

    private OptionButton _fps = null!;
    private CheckButton _reverse = null!;
    private CheckButton _fullscreen = null!;
    private CheckButton _videoEnabled = null!;
    private OptionButton _videoDecoder = null!;
    private Label _videoStatus = null!;
    private OptionButton _source = null!;

    private readonly Dictionary<string, HSlider> _sliders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckButton> _toggles = new(StringComparer.Ordinal);

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<ActiveRobotChangedEvent>();
        SubscribeRefresh<VideoStatusUpdatedEvent>();
        SubscribeRefresh<ConnectionStatusChangedEvent>();
        SubscribeRefresh<SettingsChangedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        _saveTimer = new Timer
        {
            Name = "SettingsSaveTimer",
            WaitTime = SaveDelaySeconds,
            OneShot = true,
            IgnoreTimeScale = true,
            ProcessMode = ProcessModeEnum.Always,
        };
        _saveTimer.Timeout += () => FlushSave();
        AddChild(_saveTimer);
        VisibilityChanged += OnSaveVisibilityChanged;

        var login = Body.GetNode<Control>("Login");
        TextAt(login, "登录", new Rect2(16f, 8f, 318f, 34f), 22, UiKit.Accent);
        _teamChoice = ChoiceAt(login, new[] { "红方", "蓝方" }, new Rect2(16f, 58f, 318f, 40f), OnSelectTeam);
        _robotChoice = ChoiceAt(login, new[] { "等待机器人数据" }, new Rect2(16f, 112f, 318f, 40f));
        ButtonAt(login, "登录本地演示", new Rect2(16f, 170f, 318f, 42f), OnLogin);
        _loginStatus = TextAt(login, "未绑定", new Rect2(16f, 226f, 318f, 104f), 17, UiKit.Muted);
        _loginStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var performance = Body.GetNode<Control>("Performance");
        TextAt(performance, "性能 / 图传", new Rect2(16f, 8f, 328f, 34f), 22, UiKit.Accent);
        TextAt(performance, "帧率上限", new Rect2(16f, 52f, 328f, 28f), 18);
        _fps = ChoiceAt(performance, new[] { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "240 FPS", "不限制" }, new Rect2(16f, 86f, 328f, 40f), OnSelectFps);
        _reverse = CreateToggle(performance, "反向图传", new Rect2(16f, 138f, 328f, 36f), "reverse_video");
        _fullscreen = CreateToggle(performance, "全屏显示", new Rect2(16f, 182f, 328f, 36f), "fullscreen");
        _videoEnabled = CreateToggle(performance, "图传接收 · UDP", new Rect2(16f, 226f, 328f, 36f), "video_enabled");
        _videoDecoder = ChoiceAt(performance, new[] { "解码 · 自动（硬解优先）", "解码 · CUDA", "解码 · D3D11VA", "解码 · 软件" }, new Rect2(16f, 270f, 328f, 36f), OnSelectDecoder);
        _videoStatus = TextAt(performance, string.Empty, new Rect2(16f, 314f, 328f, 36f), 13, UiKit.Muted);
        _videoStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var hardware = Body.GetNode<Control>("Hardware");
        TextAt(hardware, "硬件设置", new Rect2(16f, 8f, 328f, 34f), 22, UiKit.Accent);
        CreateSlider(hardware, "sensitivity", "控制灵敏度", 56f);
        CreateSlider(hardware, "volume", "音量", 148f);
        CreateSlider(hardware, "effect_volume", "音效音量", 240f);

        var uiSection = Body.GetNode<Control>("Interface");
        TextAt(uiSection, "UI / 操作设置", new Rect2(16f, 8f, 358f, 34f), 22, UiKit.Accent);
        _toggles["show_crosshair"] = CreateToggle(uiSection, "显示准心", new Rect2(16f, 50f, 358f, 34f), "show_crosshair");
        _toggles["show_minimap"] = CreateToggle(uiSection, "显示小地图", new Rect2(16f, 94f, 358f, 34f), "show_minimap");
        _toggles["show_custom_ui"] = CreateToggle(uiSection, "显示自定义 UI", new Rect2(16f, 138f, 358f, 34f), "show_custom_ui");
        ButtonAt(uiSection, "清除自定义 UI", new Rect2(16f, 184f, 358f, 36f), OnRequestClear);
        TextAt(uiSection, "键鼠控制来源（仅演示本地设置）", new Rect2(16f, 234f, 358f, 30f), 17, UiKit.Muted);
        _source = ChoiceAt(uiSection, new[] { "官方客户端", "自定义客户端" }, new Rect2(16f, 274f, 358f, 40f), OnSelectSource);

        ButtonAt(Body, "保存设置", new Rect2(1148f, 438f, 180f, 32f), () => FlushSave(force: true));
        ButtonAt(Body, "重新读取", new Rect2(1340f, 438f, 180f, 32f), OnLoad);
    }

    private CheckButton CreateToggle(Control parent, string title, Rect2 bounds, string key)
    {
        var control = new CheckButton
        {
            Text = title,
            FocusMode = FocusModeEnum.All,
        };
        control.AddThemeFontOverride("font", UiKit.Font());
        control.AddThemeFontSizeOverride("font_size", 18);
        control.Toggled += enabled => OnToggleSetting(enabled, key);
        parent.AddChild(control);
        UiKit.SetRect(control, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return control;
    }

    private void CreateSlider(Control parent, string key, string title, float y)
    {
        TextAt(parent, title, new Rect2(16f, y, 250f, 28f), 18);
        _values[key] = TextAt(parent, string.Empty, new Rect2(266f, y, 62f, 28f), 18, UiKit.Cyan, HorizontalAlignment.Right);
        var control = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            FocusMode = FocusModeEnum.All,
        };
        control.ValueChanged += val => OnSlideSetting((float)val, key);
        control.DragEnded += _ => FlushSave();
        parent.AddChild(control);
        UiKit.SetRect(control, 16f, y + 38f, 312f, 28f);
        _sliders[key] = control;
    }

    public override void Refresh()
    {
        if (Settings is null || Store is null)
        {
            return;
        }
        var cfg = Settings.Current;
        var state = Store.State;

        _syncing = true;
        _fps.Select(Math.Max(Array.IndexOf(FpsValues, cfg.FpsLimit), 0));
        _reverse.SetPressedNoSignal(cfg.ReverseVideo);
        _fullscreen.SetPressedNoSignal(cfg.Fullscreen);
        _videoEnabled.SetPressedNoSignal(cfg.VideoEnabled);
        _videoDecoder.Select(Math.Max(Array.IndexOf(UserSettingsData.ValidVideoDecoders, cfg.VideoDecoder), 0));
        _videoStatus.Text = $"{state.VideoLabel} · UDP {cfg.VideoPort}";
        _source.Select(cfg.ControlSource == "自定义客户端" ? 1 : 0);

        _toggles["show_crosshair"].SetPressedNoSignal(cfg.ShowCrosshair);
        _toggles["show_minimap"].SetPressedNoSignal(cfg.ShowMinimap);
        _toggles["show_custom_ui"].SetPressedNoSignal(cfg.ShowCustomUi);

        _sliders["sensitivity"].SetValueNoSignal(cfg.Sensitivity);
        _sliders["volume"].SetValueNoSignal(cfg.Volume);
        _sliders["effect_volume"].SetValueNoSignal(cfg.EffectVolume);
        foreach (var pair in _sliders)
        {
            _values[pair.Key].Text = $"{pair.Value.Value:F0}{(pair.Key != "sensitivity" ? "%" : string.Empty)}";
        }

        var teamCode = state.ActiveTeam.ToCode();
        if (_lastTeam != teamCode || _lastRobotId != state.ActiveRobotId || _robotIds.Count == 0)
        {
            _teamChoice.Select(state.ActiveTeam == TeamSide.Red ? 0 : 1);
            PopulateRobots();
            _lastTeam = teamCode;
            _lastRobotId = state.ActiveRobotId;
        }

        _loginStatus.Text = $"{state.ConnectionLabel}\n当前 {teamCode} {state.ActiveRobotId}号\n控制来源只保存本地，不发送控制链路切换。";
        _syncing = false;
    }

    private void PopulateRobots()
    {
        if (Store is null)
        {
            return;
        }
        _robotIds.Clear();
        _robotChoice.Clear();
        var side = _teamChoice.Selected == 0 ? TeamSide.Red : TeamSide.Blue;
        var team = Store.State.GetTeam(side);
        foreach (var robot in team.Robots)
        {
            _robotIds.Add(robot.Id);
            _robotChoice.AddItem($"{robot.Id}号 · {robot.Title}");
        }
        var selected = _robotIds.IndexOf(Store.State.ActiveRobotId);
        if (selected >= 0)
        {
            _robotChoice.Select(selected);
        }
    }

    private void OnSelectTeam(long _)
    {
        if (!_syncing && Store is not null)
        {
            PopulateRobots();
        }
    }

    private void OnLogin()
    {
        var index = _robotChoice.Selected;
        if (index < 0 || index >= _robotIds.Count)
        {
            SetFeedback("请选择有效机器人。", isError: true);
            return;
        }
        var robotId = _robotIds[index];
        var side = _teamChoice.Selected == 0 ? TeamSide.Red : TeamSide.Blue;
        RunCommand(c => c.SelectLogin(robotId, side));
    }

    private void OnToggleSetting(bool enabled, string key)
    {
        if (_syncing || Settings is null)
        {
            return;
        }
        var cfg = Settings.Current;
        switch (key)
        {
            case "show_crosshair": cfg.ShowCrosshair = enabled; break;
            case "show_minimap": cfg.ShowMinimap = enabled; break;
            case "show_custom_ui": cfg.ShowCustomUi = enabled; break;
            case "fullscreen": cfg.Fullscreen = enabled; break;
            case "reverse_video": cfg.ReverseVideo = enabled; break;
            case "video_enabled": cfg.VideoEnabled = enabled; break;
        }
        ApplyAndScheduleSave();
    }

    private void OnSlideSetting(float value, string key)
    {
        if (_syncing || Settings is null)
        {
            return;
        }
        var cfg = Settings.Current;
        switch (key)
        {
            case "sensitivity": cfg.Sensitivity = value; break;
            case "volume": cfg.Volume = value; break;
            case "effect_volume": cfg.EffectVolume = value; break;
        }
        ApplyAndScheduleSave();
    }

    private void OnSelectFps(long index)
    {
        if (_syncing || Settings is null || index < 0 || index >= FpsValues.Length)
        {
            return;
        }
        Settings.Current.FpsLimit = FpsValues[index];
        ApplyAndScheduleSave();
    }

    private void OnSelectDecoder(long index)
    {
        if (_syncing || Settings is null || index < 0 || index >= UserSettingsData.ValidVideoDecoders.Length)
        {
            return;
        }
        var cfg = Settings.Current;
        cfg.VideoDecoder = UserSettingsData.ValidVideoDecoders[index];
        if (cfg.VideoTransfer == "gpu" && cfg.VideoDecoder != "cuda")
        {
            cfg.VideoTransfer = "auto";
        }
        ApplyAndScheduleSave();
    }

    private void OnSelectSource(long index)
    {
        if (_syncing || Settings is null)
        {
            return;
        }
        Settings.Current.ControlSource = index == 0 ? "官方客户端" : "自定义客户端";
        ApplyAndScheduleSave();
    }

    private void ApplyAndScheduleSave()
    {
        if (Settings is null)
        {
            return;
        }
        if (!_savePending)
        {
            SetFeedback("设置已应用，等待自动保存。");
        }
        _savePending = true;
        _saveTimer.Start();
        ApplyRuntime();
        Settings.NotifyChanged();
    }

    private void OnSaveVisibilityChanged()
    {
        if (!IsVisibleInTree())
        {
            FlushSave();
        }
    }

    public override void SetActive(bool active)
    {
        if (!active)
        {
            FlushSave();
        }
        base.SetActive(active);
    }

    protected override void RequestClose()
    {
        FlushSave();
        base.RequestClose();
    }

    public override void _ExitTree()
    {
        FlushSave();
        base._ExitTree();
    }

    private void ApplyRuntime()
    {
        if (Settings is null)
        {
            return;
        }
        var cfg = Settings.Current;
        Engine.MaxFps = cfg.FpsLimit;
        var window = GetWindow();
        if (window is not null)
        {
            if (cfg.Fullscreen && window.Mode != Window.ModeEnum.Fullscreen && window.Mode != Window.ModeEnum.ExclusiveFullscreen)
            {
                window.Mode = Window.ModeEnum.Fullscreen;
            }
            else if (!cfg.Fullscreen && (window.Mode == Window.ModeEnum.Fullscreen || window.Mode == Window.ModeEnum.ExclusiveFullscreen))
            {
                window.Mode = Window.ModeEnum.Windowed;
            }
        }
        if (AudioServer.BusCount > 0)
        {
            AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(MathF.Max(cfg.Volume / 100f, 0.0001f)));
        }
        var effectsBus = AudioServer.GetBusIndex("SFX");
        if (effectsBus >= 0)
        {
            AudioServer.SetBusVolumeDb(effectsBus, Mathf.LinearToDb(MathF.Max(cfg.EffectVolume / 100f, 0.0001f)));
        }
    }

    public int FlushSave(bool force = false)
    {
        if (_saveTimer is not null && IsInstanceValid(_saveTimer))
        {
            _saveTimer.Stop();
        }
        if (Settings is null || (!_savePending && !force))
        {
            return 0;
        }
        var err = Settings.Save();
        _savePending = err != 0;
        SetFeedback(err == 0 ? "本地设置已保存；控制来源不发送。" : $"设置保存失败，错误 {err}。", isError: err != 0);
        return err;
    }

    private void OnLoad()
    {
        if (Settings is null)
        {
            return;
        }
        if (FlushSave() != 0)
        {
            return;
        }
        var err = Settings.Load();
        if (err == 0)
        {
            ApplyRuntime();
            RefreshIfReady();
        }
        SetFeedback(err == 0 ? "已重新读取本地设置。" : $"设置读取失败，错误 {err}。", isError: err != 0);
    }

    private void OnRequestClear()
    {
        ClearRequested?.Invoke();
        SetFeedback("已请求主场景清除自定义 UI。");
    }
}
