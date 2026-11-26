using System;
using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Segmented status bar for health, shields, and energy strips.
/// </summary>
[Tool]
public partial class BeveledBar : Control
{
    private bool _hasValue;
    private float _displayRatio = 1f;
    private Color _tint = new("cc6a60");
    private float _ratio = 1f;
    private float _segment = 20f;
    private float _gap = 3f;

    public float DisplayRatio
    {
        get => _displayRatio;
        set
        {
            if (MathF.Abs(_displayRatio - value) < 0.0001f)
            {
                return;
            }
            _displayRatio = value;
            QueueRedraw();
        }
    }

    [Export]
    public Color Tint
    {
        get => _tint;
        set
        {
            if (_tint == value)
            {
                return;
            }
            _tint = value;
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0.0,1.0")]
    public float Ratio
    {
        get => _ratio;
        set
        {
            var next = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(_ratio - next) < 0.0001f)
            {
                _hasValue = _hasValue || IsNodeReady();
                return;
            }
            _ratio = next;
            if (!_hasValue || !IsNodeReady() || !IsVisibleInTree())
            {
                SnapToValue();
            }
            else
            {
                UiMotion.Animate(this, new NodePath(nameof(DisplayRatio)), Variant.From(_ratio), UiMotion.ValueTime, "value");
            }
            _hasValue = IsNodeReady();
        }
    }

    [Export]
    public bool Reverse { get; set; }

    [Export]
    public bool Invulnerable { get; set; }

    [Export]
    public float Segment
    {
        get => _segment;
        set
        {
            _segment = MathF.Max(value, 0f);
            QueueRedraw();
        }
    }

    [Export]
    public float Gap
    {
        get => _gap;
        set
        {
            _gap = MathF.Max(value, 0f);
            QueueRedraw();
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
        DisplayRatio = _ratio;
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
        var fillWidth = Size.X * _displayRatio;
        var fillColor = new Color(_tint, 0.92f);
        if (_segment <= 0f || _segment <= _gap + 1f)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), UiKit.Track);
            DrawSpan(0f, fillWidth, fillColor);
        }
        else
        {
            var x = 0f;
            while (x < Size.X - 0.5f)
            {
                var cell = new Rect2(x, 0f, MathF.Min(_segment - _gap, Size.X - x), Size.Y);
                DrawRect(cell, UiKit.Track);
                var start = cell.Position.X;
                var end = MathF.Min(cell.End.X, fillWidth);
                if (end > start)
                {
                    DrawSpan(start, end, fillColor);
                }
                x += _segment;
            }
        }

        if (Invulnerable)
        {
            DrawRect(new Rect2(new Vector2(-1f, -1f), Size + new Vector2(2f, 2f)), UiKit.Gold, false, 1f);
        }
    }

    private void DrawSpan(float start, float end, Color color)
    {
        if (end <= start)
        {
            return;
        }
        var rect = new Rect2(start, 0f, end - start, Size.Y);
        if (Reverse)
        {
            rect.Position = new Vector2(Size.X - end, 0f);
        }
        DrawRect(rect, color);
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, MathF.Min(1f, Size.Y * 0.2f))), new Color(1f, 1f, 1f, 0.18f));
    }
}
