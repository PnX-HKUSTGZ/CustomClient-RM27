using System;
using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Dark slate plate with one slanted edge, a top accent tick, and a subtle bottom mark.
/// Cut > 0 slants the right edge; Cut < 0 slants the left edge.
/// </summary>
[Tool]
public partial class HudPlate : Panel
{
    private float _cut = 14f;
    private Color _fillColor = new(0.031f, 0.047f, 0.071f, 0.78f);
    private Color _edgeColor = new(0.769f, 0.541f, 0.322f, 0.75f);

    [Export]
    public float Cut
    {
        get => _cut;
        set
        {
            _cut = value;
            QueueRedraw();
        }
    }

    [Export]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            QueueRedraw();
        }
    }

    [Export]
    public Color EdgeColor
    {
        get => _edgeColor;
        set
        {
            _edgeColor = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var slant = MathF.Abs(_cut);
        var points = _cut >= 0f
            ? new[] { new Vector2(0, 0), new Vector2(Size.X, 0), new Vector2(Size.X - slant, Size.Y), new Vector2(0, Size.Y) }
            : new[] { new Vector2(0, 0), new Vector2(Size.X, 0), new Vector2(Size.X, Size.Y), new Vector2(slant, Size.Y) };

        DrawColoredPolygon(points, _fillColor);
        var tickX = _cut < 0f ? slant : 0f;
        DrawRect(new Rect2(tickX, 0, MathF.Min(28f, Size.X * 0.3f), 2f), _edgeColor);
        var mark = new Color(UiKit.LineStrong, 0.5f);
        var right = Size.X - (_cut >= 0f ? slant : 0f);
        DrawLine(new Vector2(right - 8f, Size.Y - 1f), new Vector2(right, Size.Y - 1f), mark, 1f);
    }
}
