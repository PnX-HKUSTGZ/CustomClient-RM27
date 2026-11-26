using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Tactical arena map supporting robot positions and local operator markers.
/// Subscribes only to relevant map/robot domain events and avoids redundant redraws when hidden.
/// </summary>
[Tool]
public partial class ArenaMap : Control
{
    public event Action<Vector2, string>? MarkerRequested;
    public event Action? MarkersChanged;

    public const int MaxMarkers = 12;

    private bool _interactive;

    [Export]
    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            if (IsNodeReady())
            {
                MouseDefaultCursorShape = _interactive ? CursorShape.Cross : CursorShape.Arrow;
                MouseFilter = _interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            }
        }
    }

    [Export]
    public string MarkerKind { get; set; } = "attack";

    [Export]
    public Texture2D? MapTexture { get; set; }

    private TerminalStore? _store;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly List<(Vector2 Position, string Kind)> _markers = new();
    private string _drawSignature = string.Empty;

    public override void _Ready()
    {
        MapTexture ??= ResourceLoader.Load<Texture2D>("res://assets/reference/arena_map.png");
        MouseDefaultCursorShape = _interactive ? CursorShape.Cross : CursorShape.Arrow;
        MouseFilter = _interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Resized += () =>
        {
            if (IsVisibleInTree())
            {
                QueueRedraw();
            }
        };
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _ExitTree()
    {
        ClearSubscriptions();
    }

    public void Bind(TerminalStore store)
    {
        ClearSubscriptions();
        _store = store;
        var bus = store.Bus;
        _subscriptions.Add(bus.Subscribe<ActiveRobotChangedEvent>(_ => RefreshGeometryIfChanged()));
        _subscriptions.Add(bus.Subscribe<RobotHealthUpdatedEvent>(_ => RefreshGeometryIfChanged()));
        _subscriptions.Add(bus.Subscribe<RobotPositionUpdatedEvent>(_ => RefreshGeometryIfChanged()));
        _subscriptions.Add(bus.Subscribe<DemoStateResetEvent>(_ => RefreshGeometryIfChanged()));
        _drawSignature = string.Empty;
        RefreshGeometryIfChanged();
    }

    private void ClearSubscriptions()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }

    private void RefreshGeometryIfChanged()
    {
        if (_store is null || !IsVisibleInTree())
        {
            return;
        }
        var state = _store.State;
        var sig = $"{state.ActiveTeam}:{state.ActiveRobotId}";
        foreach (var side in new[] { TeamSide.Red, TeamSide.Blue })
        {
            foreach (var robot in state.GetTeam(side).Robots)
            {
                if (robot.Health > 0)
                {
                    sig += $"|{side}:{robot.Id}:{robot.MapPosition.X:F4},{robot.MapPosition.Y:F4}";
                }
            }
        }
        if (sig != _drawSignature)
        {
            _drawSignature = sig;
            QueueRedraw();
        }
    }

    private void OnVisibilityChanged()
    {
        if (IsVisibleInTree())
        {
            _drawSignature = string.Empty;
            RefreshGeometryIfChanged();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color(0.031f, 0.047f, 0.071f, 0.78f));
        if (MapTexture is not null)
        {
            DrawTextureRect(MapTexture, bounds, false, new Color(0.62f, 0.66f, 0.72f, 0.82f));
        }
        DrawRect(bounds, new Color(0.02f, 0.03f, 0.05f, 0.22f));
        DrawRect(bounds, UiKit.Line, false, 1f);
        UiKit.DrawBrackets(this, bounds.Grow(3f), UiKit.LineStrong, Size.X > 600f ? 14f : 10f, 1.5f);
        DrawRect(new Rect2(0f, -3f, MathF.Min(36f, Size.X * 0.2f), 2f), UiKit.Accent);

        if (_store is not null)
        {
            var state = _store.State;
            foreach (var side in new[] { TeamSide.Red, TeamSide.Blue })
            {
                var team = state.GetTeam(side);
                var color = side == TeamSide.Red ? UiKit.Red : UiKit.Blue;
                foreach (var robot in team.Robots)
                {
                    if (robot.Health <= 0)
                    {
                        continue;
                    }
                    var point = new Vector2(robot.MapPosition.X * Size.X, robot.MapPosition.Y * Size.Y);
                    var radius = Size.X > 600f ? 14f : 8f;
                    DrawCircle(point, radius + 2f, new Color(0.02f, 0.03f, 0.05f, 0.75f));
                    DrawCircle(point, radius, new Color(color, 0.9f));
                    var fontSize = Size.X > 600f ? 19 : 12;
                    DrawString(UiKit.Font(), point + new Vector2(-fontSize * 0.3f, fontSize * 0.38f), robot.Id.ToString(), HorizontalAlignment.Left, -1, fontSize, Colors.White);
                    if (side == state.ActiveTeam && robot.Id == state.ActiveRobotId)
                    {
                        DrawArc(point, radius + 5f, 0f, Mathf.Tau, 32, UiKit.Gold, 2f, true);
                    }
                }
            }
        }

        foreach (var marker in _markers)
        {
            var point = marker.Position * Size;
            MapMarkerIcon.DrawIcon(this, marker.Kind, point, Size.X > 600f ? 19f : 11f);
        }
    }

    public int MarkerCount => _markers.Count;

    public bool AddMarker(Vector2 position, string kind)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)
            || position.X < 0f || position.X > 1f || position.Y < 0f || position.Y > 1f
            || Array.IndexOf(MapMarkerIcon.Kinds, kind) < 0)
        {
            return false;
        }
        _markers.Add((position, kind));
        if (_markers.Count > MaxMarkers)
        {
            _markers.RemoveAt(0);
        }
        MarkersChanged?.Invoke();
        QueueRedraw();
        return true;
    }

    public bool UndoLastMarker()
    {
        if (_markers.Count == 0)
        {
            return false;
        }
        _markers.RemoveAt(_markers.Count - 1);
        MarkersChanged?.Invoke();
        QueueRedraw();
        return true;
    }

    public int ClearMarkers()
    {
        var removed = _markers.Count;
        if (removed > 0)
        {
            _markers.Clear();
            MarkersChanged?.Invoke();
            QueueRedraw();
        }
        return removed;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_interactive || !IsVisibleInTree() || @event is not InputEventMouseButton mouse || !mouse.Pressed)
        {
            return;
        }
        if (!new Rect2(Vector2.Zero, Size).HasPoint(mouse.Position))
        {
            return;
        }

        switch (mouse.ButtonIndex)
        {
            case MouseButton.Left:
                if (Size.X <= 0f || Size.Y <= 0f)
                {
                    return;
                }
                MarkerRequested?.Invoke(mouse.Position / Size, MarkerKind);
                AcceptEvent();
                break;
            case MouseButton.Right:
                UndoLastMarker();
                AcceptEvent();
                break;
        }
    }
}
