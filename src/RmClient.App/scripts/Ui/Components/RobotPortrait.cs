using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.State;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Hex-framed portrait for the active robot in the bottom HUD.
/// </summary>
[Tool]
public partial class RobotPortrait : Control
{
    private static readonly Dictionary<int, string> RobotTypes = new()
    {
        [1] = "hero",
        [2] = "engineer",
        [3] = "infantry",
        [4] = "infantry",
        [6] = "aerial",
        [7] = "sentry",
    };

    private int _robotId = 3;
    private string _side = "red";
    private bool _updatingIdentity;
    private int _loadedId = -1;
    private string _loadedSide = string.Empty;

    public Texture2D? PortraitTexture { get; private set; }

    [Export]
    public int RobotId
    {
        get => _robotId;
        set
        {
            if (_robotId == value)
            {
                return;
            }
            _robotId = value;
            if (!_updatingIdentity)
            {
                UpdateTexture();
            }
        }
    }

    [Export(PropertyHint.Enum, "red,blue")]
    public string Side
    {
        get => _side;
        set
        {
            if (_side == value)
            {
                return;
            }
            _side = value;
            if (!_updatingIdentity)
            {
                UpdateTexture();
            }
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        UpdateTexture();
    }

    public void UpdateRobot(RobotState robot, TeamSide teamSide)
    {
        var sideCode = teamSide.ToCode();
        _updatingIdentity = true;
        RobotId = robot.Id;
        Side = sideCode;
        _updatingIdentity = false;
        UpdateTexture();

        var nextTooltip = $"{(teamSide == TeamSide.Red ? "红" : "蓝")}方 {robot.Id}号 · {robot.Title}";
        if (TooltipText != nextTooltip)
        {
            TooltipText = nextTooltip;
        }
    }

    private void UpdateTexture()
    {
        if (_loadedId == _robotId && _loadedSide == _side)
        {
            return;
        }
        _loadedId = _robotId;
        _loadedSide = _side;
        if (RobotTypes.TryGetValue(_robotId, out var kind))
        {
            var path = $"res://assets/portraits/{kind}_{_side}.png";
            PortraitTexture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }
        else
        {
            PortraitTexture = null;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var diameter = MathF.Min(Size.X, Size.Y);
        var center = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
        var color = _side == "red" ? UiKit.Red : UiKit.Blue;
        var radius = diameter * 0.47f;

        var hex = new Vector2[6];
        var hexOutline = new Vector2[7];
        for (var i = 0; i < 6; i++)
        {
            var angle = -MathF.PI / 2f + i * Mathf.Tau / 6f;
            var pt = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            hex[i] = pt;
            hexOutline[i] = pt;
        }
        hexOutline[6] = hex[0];

        DrawColoredPolygon(hex, new Color(0.035f, 0.05f, 0.07f, 0.86f));
        if (PortraitTexture is not null)
        {
            var available = Vector2.One * diameter * 0.74f;
            var scaleFactor = MathF.Min(available.X / PortraitTexture.GetWidth(), available.Y / PortraitTexture.GetHeight());
            var imageSize = PortraitTexture.GetSize() * scaleFactor;
            DrawTextureRect(PortraitTexture, new Rect2(center - imageSize * 0.5f, imageSize), false, new Color(0.92f, 0.92f, 0.92f, 1f));
        }
        DrawPolyline(hexOutline, new Color(color, 0.8f), 1.6f, true);
        DrawLine(hex[0] + new Vector2(-10f, 0f), hex[0] + new Vector2(10f, 0f), UiKit.Accent, 2f);
    }
}
