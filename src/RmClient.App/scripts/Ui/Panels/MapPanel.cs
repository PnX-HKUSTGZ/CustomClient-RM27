using System;
using Godot;
using RmClient.App.Theme;
using RmClient.App.Ui.Components;
using RmClient.Core.Commands;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Settings;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class MapPanel : TerminalPanel
{
    private ArenaMap _map = null!;
    private OptionButton _kind = null!;
    private OptionButton _receiver = null!;
    private int _receiverId = -1;
    private Button _undoButton = null!;
    private Button _clearButton = null!;
    private int _markerTotal;

    public ArenaMap ArenaMapView => _map;

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<ActiveRobotChangedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        var area = Body.GetNode<Control>("MapArea");
        _map = new ArenaMap
        {
            Name = "ArenaMap",
            Interactive = true,
            FocusMode = FocusModeEnum.All,
        };
        area.AddChild(_map);
        UiKit.SetRect(_map, 1f, 1f, 1068f, 586f);
        _map.MarkerRequested += OnMarkerRequested;
        _map.GuiInput += OnMapGuiInput;

        var section = Body.GetNode<Control>("MarkerControls");
        TextAt(section, "地图标记", new Rect2(12f, 4f, 192f, 40f), 22, UiKit.Accent);
        _kind = ChoiceAt(section, new[] { "A  进攻", "B  防守", "I  警戒" }, new Rect2(12f, 54f, 192f, 42f), idx => SelectKind((int)idx));
        TextAt(section, "接收目标", new Rect2(12f, 118f, 192f, 32f), 19);
        _receiver = ChoiceAt(section, new[] { "当前机器人" }, new Rect2(12f, 158f, 192f, 42f), _ => RefreshIfReady());

        string[] titles = { "A  进攻", "B  防守", "I  警戒" };
        for (var i = 0; i < MapMarkerIcon.Kinds.Length; i++)
        {
            var kind = MapMarkerIcon.Kinds[i];
            var icon = new MapMarkerIcon { Kind = kind };
            section.AddChild(icon);
            UiKit.SetRect(icon, 12f, 210f + i * 34f, 32f, 32f);
            TextAt(section, titles[i], new Rect2(52f, 210f + i * 34f, 152f, 32f), 17, MapMarkerIcon.KindColors[kind]);
        }

        _undoButton = ButtonAt(section, "撤回最近标记", new Rect2(12f, 324f, 192f, 40f), OnUndoMarker);
        _clearButton = ButtonAt(section, "清空标记", new Rect2(12f, 374f, 192f, 40f), OnClearMarkers);

        var note = TextAt(section, "左键标记，右键撤回最近一个。\n仅清除本地显示，不撤销已发指令。\n当前接收目标为本机。", new Rect2(12f, 426f, 192f, 150f), 16, UiKit.Muted);
        note.Name = "MarkerInstructions";
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.VerticalAlignment = VerticalAlignment.Top;
        note.Size = new Vector2(192f, 150f);

        _markerTotal = _map.MarkerCount;
        _map.MarkersChanged += OnMarkersChanged;
        UpdateMarkerButtons();
        SetProcessUnhandledKeyInput(false);
    }

    public override void Bind(TerminalStore store, TerminalCommands commands, UserSettingsService settings)
    {
        base.Bind(store, commands, settings);
        if (_map is not null && Store is not null)
        {
            _map.Bind(Store);
        }
    }

    public override void Refresh()
    {
        if (_map is null || Store is null)
        {
            return;
        }
        var robot = Store.State.GetActiveRobot();
        _receiverId = robot?.Id ?? -1;
        _receiver.SetItemText(0, robot is not null ? $"{robot.Id}号 {robot.Title}（本机）" : "无有效接收目标");
    }

    public override void SetActive(bool active)
    {
        base.SetActive(active);
        SetProcessUnhandledKeyInput(active);
        if (_map is not null)
        {
            _map.Interactive = active;
        }
    }

    private void SelectKind(int index)
    {
        var clamped = Math.Clamp(index, 0, 2);
        _map.MarkerKind = MapMarkerIcon.Kinds[clamped];
        _kind.Select(clamped);
    }

    private void OnMarkerRequested(Vector2 position, string kind)
    {
        if (!IsVisibleInTree() || _receiverId < 0)
        {
            SetFeedback("没有有效接收目标，未发送。", isError: true);
            return;
        }
        var point = new NormalizedPoint(position.X, position.Y);
        if (RunCommand(c => c.SubmitMapMarker(point, kind, _receiverId)))
        {
            _map.AddMarker(position, kind);
        }
    }

    private void OnUndoMarker()
    {
        if (!IsVisibleInTree())
        {
            return;
        }
        if (!_map.UndoLastMarker())
        {
            SetFeedback("没有可撤回的本地标记。");
        }
    }

    private void OnClearMarkers()
    {
        if (!IsVisibleInTree())
        {
            return;
        }
        var removed = _map.ClearMarkers();
        SetFeedback($"已清空 {removed} 个本地标记；不撤销已发送指令。");
    }

    private void OnMarkersChanged()
    {
        var count = _map.MarkerCount;
        if (count < _markerTotal && IsVisibleInTree())
        {
            SetFeedback($"已撤回本地标记，剩余 {count} 个；不撤销已发送指令。");
        }
        _markerTotal = count;
        UpdateMarkerButtons();
    }

    private void UpdateMarkerButtons()
    {
        var empty = _map.MarkerCount == 0;
        _undoButton.Disabled = empty;
        _clearButton.Disabled = empty;
    }

    private void OnMapGuiInput(InputEvent @event)
    {
        if (HandleKey(@event))
        {
            _map.AcceptEvent();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (HandleKey(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool HandleKey(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventKey key)
        {
            return false;
        }
        if (!key.Pressed || key.Echo || key.CtrlPressed || key.AltPressed || key.MetaPressed)
        {
            return false;
        }
        switch (key.Keycode)
        {
            case Key.A:
                SelectKind(0);
                return true;
            case Key.B:
                SelectKind(1);
                return true;
            case Key.I:
                SelectKind(2);
                return true;
            default:
                return false;
        }
    }
}
