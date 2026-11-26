using System;
using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Central aiming reticle with four ticks, accent pip, faint range brackets, and outer heat arc.
/// </summary>
[Tool]
public partial class OperatorCrosshair : Control
{
    private bool _hasValue;
    private float _displayHeat;
    private float _heatRatio;

    public float DisplayHeat
    {
        get => _displayHeat;
        set
        {
            if (MathF.Abs(_displayHeat - value) < 0.0001f)
            {
                return;
            }
            _displayHeat = value;
            QueueRedraw();
        }
    }

    [Export]
    public float HeatRatio
    {
        get => _heatRatio;
        set
        {
            var next = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(_heatRatio - next) < 0.0001f)
            {
                _hasValue = _hasValue || IsNodeReady();
                return;
            }
            _heatRatio = next;
            if (!_hasValue || !IsNodeReady() || !IsVisibleInTree())
            {
                SnapToValue();
            }
            else
            {
                UiMotion.Animate(this, new NodePath(nameof(DisplayHeat)), Variant.From(_heatRatio), UiMotion.ValueTime, "value");
            }
            _hasValue = IsNodeReady();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        VisibilityChanged += OnVisibilityChanged;
    }

    public void SnapToValue()
    {
        UiMotion.Cancel(this, "value");
        DisplayHeat = _heatRatio;
    }

    private void OnVisibilityChanged()
    {
        if (!IsVisibleInTree())
        {
            SnapToValue();
        }
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var shade = new Color(0.02f, 0.03f, 0.045f, 0.7f);
        var ink = new Color(0.93f, 0.95f, 0.97f, 0.92f);

        var frame = new Rect2(center - new Vector2(150f, 96f), new Vector2(300f, 192f));
        UiKit.DrawBrackets(this, frame, new Color(1f, 1f, 1f, 0.1f), 18f, 1f);
        DrawArc(center, 20f, 0f, Mathf.Tau, 64, shade, 5f, true);
        DrawArc(center, 20f, 0f, Mathf.Tau, 64, ink, 1.8f, true);

        foreach (var direction in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
        {
            DrawLine(center + direction * 36f, center + direction * 51f, shade, 5f);
            DrawLine(center + direction * 36f, center + direction * 51f, ink, 2.2f);
        }

        DrawRect(new Rect2(center - new Vector2(2.5f, 2.5f), new Vector2(5f, 5f)), UiKit.Accent);
        DrawArc(center, 28f, -MathF.PI / 2f, -MathF.PI / 2f + Mathf.Tau, 64, new Color(1f, 1f, 1f, 0.08f), 3f, true);

        if (_displayHeat > 0f)
        {
            var color = _heatRatio < 0.5f ? UiKit.Cyan : UiKit.Gold;
            if (_heatRatio > 0.75f)
            {
                color = UiKit.Red;
            }
            DrawArc(center, 28f, -MathF.PI / 2f, -MathF.PI / 2f + Mathf.Tau * _displayHeat, 64, new Color(color, 0.85f), 3f, true);
        }
    }
}
