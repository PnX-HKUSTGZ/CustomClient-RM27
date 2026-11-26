using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Vector icon for tactical map markers (attack crosshair, defend shield, alert triangle).
/// </summary>
[Tool]
public partial class MapMarkerIcon : Control
{
    public static readonly string[] Kinds = { "attack", "defend", "alert" };

    public static readonly IReadOnlyDictionary<string, Color> KindColors = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        ["attack"] = UiKit.Red,
        ["defend"] = UiKit.Blue,
        ["alert"] = UiKit.Gold,
    };

    private string _kind = "attack";

    [Export(PropertyHint.Enum, "attack,defend,alert")]
    public string Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        DrawIcon(this, _kind, Size * 0.5f, MathF.Min(Size.X, Size.Y) * 0.35f);
    }

    public static void DrawIcon(CanvasItem canvas, string markerKind, Vector2 center, float radius)
    {
        var color = KindColors.TryGetValue(markerKind, out var mapped) ? mapped : UiKit.Gold;
        var stroke = MathF.Max(2f, radius * 0.14f);
        var background = new Color(0.031f, 0.047f, 0.071f, 0.94f);

        switch (markerKind)
        {
            case "attack":
                canvas.DrawCircle(center, radius, background);
                canvas.DrawArc(center, radius, 0f, Mathf.Tau, 40, color, stroke, true);
                foreach (var dir in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
                {
                    canvas.DrawLine(center + dir * radius * 0.6f, center + dir * radius * 1.35f, color, stroke, true);
                }
                canvas.DrawCircle(center, stroke, color);
                break;

            case "defend":
                var shield = new[]
                {
                    center + new Vector2(0f, -1f) * radius,
                    center + new Vector2(0.85f, -0.65f) * radius,
                    center + new Vector2(0.78f, 0.18f) * radius,
                    center + new Vector2(0.48f, 0.72f) * radius,
                    center + new Vector2(0f, 1f) * radius,
                    center + new Vector2(-0.48f, 0.72f) * radius,
                    center + new Vector2(-0.78f, 0.18f) * radius,
                    center + new Vector2(-0.85f, -0.65f) * radius,
                };
                var shieldOutline = new Vector2[shield.Length + 1];
                Array.Copy(shield, shieldOutline, shield.Length);
                shieldOutline[^1] = shield[0];
                canvas.DrawColoredPolygon(shield, background);
                canvas.DrawPolyline(shieldOutline, color, stroke, true);
                canvas.DrawLine(center + new Vector2(0f, -0.5f) * radius, center + new Vector2(0f, 0.5f) * radius, color, stroke, true);
                break;

            case "alert":
                var triangle = new[]
                {
                    center + new Vector2(0f, -1.1f) * radius,
                    center + new Vector2(1f, 0.85f) * radius,
                    center + new Vector2(-1f, 0.85f) * radius,
                };
                var triOutline = new[] { triangle[0], triangle[1], triangle[2], triangle[0] };
                canvas.DrawColoredPolygon(triangle, background);
                canvas.DrawPolyline(triOutline, color, stroke, true);
                canvas.DrawLine(center + new Vector2(0f, -0.4f) * radius, center + new Vector2(0f, 0.18f) * radius, color, stroke * 1.4f, true);
                canvas.DrawCircle(center + new Vector2(0f, 0.52f) * radius, stroke * 0.8f, color);
                break;
        }
    }
}
