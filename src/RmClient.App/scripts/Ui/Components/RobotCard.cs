using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.State;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Squad status row displaying hex number badge, role name, segmented health strip, and numeric health.
/// Blue rows mirror horizontally so both columns face inward.
/// </summary>
[Tool]
public partial class RobotCard : Button
{
    private const float Slant = 14f;

    private static readonly Dictionary<int, string> RedPortraitPaths = new()
    {
        [1] = "res://assets/reference/robot_hero.png",
        [2] = "res://assets/reference/robot_engineer.png",
        [3] = "res://assets/reference/robot_infantry.png",
        [4] = "res://assets/reference/robot_infantry.png",
        [6] = "res://assets/reference/robot_aerial.png",
        [7] = "res://assets/reference/robot_sentry.png",
    };

    private static readonly Dictionary<int, string> BluePortraitPaths = new()
    {
        [1] = "res://assets/reference/robot_hero_blue.png",
        [2] = "res://assets/reference/robot_engineer_blue.png",
        [3] = "res://assets/reference/robot_infantry_blue.png",
        [4] = "res://assets/reference/robot_infantry_blue.png",
        [6] = "res://assets/reference/robot_aerial_blue.png",
        [7] = "res://assets/reference/robot_sentry_blue.png",
    };

    private static readonly Dictionary<string, Texture2D?> PortraitCache = new(StringComparer.Ordinal);

    private string _side = "red";
    private float _hoverAmount;
    private RobotState? _robot;
    private bool _selected;
    private string _drawSignature = string.Empty;

    [Export]
    public int RobotId { get; set; } = 3;

    [Export]
    public string RobotTitle { get; set; } = "步兵";

    [Export]
    public string Side
    {
        get => _side;
        set
        {
            _side = value;
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0.0,1.0")]
    public float HoverAmount
    {
        get => _hoverAmount;
        set
        {
            var next = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(_hoverAmount - next) < 0.0001f)
            {
                return;
            }
            _hoverAmount = next;
            QueueRedraw();
        }
    }

    public static Texture2D? GetScoreboardPortrait(int robotId, TeamSide side)
    {
        var map = side == TeamSide.Red ? RedPortraitPaths : BluePortraitPaths;
        var path = map.TryGetValue(robotId, out var matched) ? matched : map[3];
        if (!PortraitCache.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            PortraitCache[path] = texture;
        }
        return texture;
    }

    public override void _Ready()
    {
        Flat = true;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "切换查看此机器人（本地演示）";
        MouseEntered += () => UiMotion.Animate(this, new NodePath(nameof(HoverAmount)), Variant.From(1f), UiMotion.FeedbackTime, "hover");
        MouseExited += () => UiMotion.Animate(this, new NodePath(nameof(HoverAmount)), Variant.From(0f), UiMotion.FeedbackTime, "hover");
        Pressed += () => UiMotion.Pulse(this);
        Resized += QueueRedraw;
    }

    public void UpdateRobot(RobotState value, bool active)
    {
        var signature = $"{value.Id}|{value.Title}|{_side}|{value.Health}|{value.MaxHealth}|{value.Ammo}|{value.Online}|{active}";
        _robot = value;
        RobotId = value.Id;
        RobotTitle = value.Title;
        _selected = active;
        if (signature != _drawSignature)
        {
            _drawSignature = signature;
            QueueRedraw();
        }
    }

    private float MirrorX(float value) => _side == "blue" ? Size.X - value : value;

    public override void _Draw()
    {
        var teamColor = _side == "red" ? UiKit.Red : UiKit.Blue;
        var w = Size.X;
        var h = Size.Y;
        var plate = new[]
        {
            new Vector2(MirrorX(0f), 0f),
            new Vector2(MirrorX(w - Slant), 0f),
            new Vector2(MirrorX(w), h),
            new Vector2(MirrorX(0f), h),
        };

        var fill = new Color(0.031f, 0.047f, 0.071f, 0.78f);
        if (_selected)
        {
            fill = new Color(0.07f, 0.075f, 0.08f, 0.88f);
        }
        else if (_hoverAmount > 0f)
        {
            fill = fill.Lerp(new Color(0.09f, 0.11f, 0.14f, 0.86f), _hoverAmount);
        }
        DrawColoredPolygon(plate, fill);

        if (_selected)
        {
            DrawRect(new Rect2(MirrorX(0f) - (_side == "blue" ? 3f : 0f), 0f, 3f, h), UiKit.Accent);
        }

        var online = _robot is null || _robot.Online;
        var health = _robot?.Health ?? 0;
        var maxHealth = _robot is null ? 1 : Math.Max(_robot.MaxHealth, 1);
        var ratio = Math.Clamp((float)health / maxHealth, 0f, 1f);
        var badgeRadius = MathF.Min(h * 0.36f, 19f);

        UiKit.DrawHexBadge(
            this,
            new Vector2(MirrorX(12f + badgeRadius), h * 0.5f),
            badgeRadius,
            online ? teamColor : UiKit.Dim,
            RobotId.ToString(),
            (int)(badgeRadius * 0.95f));

        var font = UiKit.Font();
        var textLeft = 24f + badgeRadius * 2f;
        var barWidth = w - textLeft - 72f;
        var nameText = $"{RobotTitle} {RobotId}";
        var nameSize = font.GetStringSize(nameText, HorizontalAlignment.Left, -1, 16);
        var nameX = _side == "red" ? textLeft : w - textLeft - nameSize.X;
        DrawString(font, new Vector2(nameX, h * 0.42f), nameText, HorizontalAlignment.Left, -1, 16, online ? UiKit.Text : UiKit.Dim);

        var ammoText = $"≡ {_robot?.Ammo ?? 0}";
        var ammoSize = font.GetStringSize(ammoText, HorizontalAlignment.Left, -1, 12);
        var ammoX = _side == "red" ? textLeft + barWidth - ammoSize.X : w - textLeft - barWidth;
        DrawString(font, new Vector2(ammoX, h * 0.42f), ammoText, HorizontalAlignment.Left, -1, 12, UiKit.Muted);

        var barColor = UiKit.Text.Darkened(0.08f);
        if (ratio <= 0.3f)
        {
            barColor = UiKit.Red;
        }
        else if (ratio <= 0.6f)
        {
            barColor = UiKit.Gold;
        }

        var barY = h * 0.58f;
        const float pitch = 12f;
        var x = 0f;
        while (x < barWidth - 0.5f)
        {
            var cellWidth = MathF.Min(pitch - 2f, barWidth - x);
            var cellX = _side == "red" ? textLeft + x : w - textLeft - x - cellWidth;
            DrawRect(new Rect2(cellX, barY, cellWidth, 7f), UiKit.Track);
            var filled = Math.Clamp(barWidth * ratio - x, 0f, cellWidth);
            if (filled > 0f)
            {
                var filledX = _side == "red" ? cellX : cellX + cellWidth - filled;
                DrawRect(new Rect2(filledX, barY, filled, 7f), new Color(barColor, 0.92f));
            }
            x += pitch;
        }

        var valueText = online ? health.ToString() : "离线";
        var valueSize = font.GetStringSize(valueText, HorizontalAlignment.Left, -1, 20);
        var valueX = _side == "red" ? w - Slant - 8f - valueSize.X : Slant + 8f;
        var valueColor = UiKit.Text;
        if (!online)
        {
            valueColor = UiKit.Dim;
        }
        else if (ratio <= 0.3f)
        {
            valueColor = UiKit.Red.Lightened(0.15f);
        }
        DrawString(font, new Vector2(valueX, h * 0.5f + 7f), valueText, HorizontalAlignment.Left, -1, 20, valueColor);

        if (!online)
        {
            DrawColoredPolygon(plate, new Color(0f, 0f, 0f, 0.35f));
        }
    }
}
