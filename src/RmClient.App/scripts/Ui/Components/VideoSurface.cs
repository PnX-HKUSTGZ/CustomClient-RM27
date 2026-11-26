using System;
using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Components;

/// <summary>
/// Video background container. Hosts a native decoder surface (RmVideoSurface) as a full-rect child,
/// while retaining Texture2D presentation for alternative sources and a placeholder backdrop when idle.
/// </summary>
[Tool]
public partial class VideoSurface : Control
{
    private Texture2D? _texture;
    private bool _reverse;
    private bool _signalPattern = true;
    private Control? _native;
    private bool _live;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        Resized += OnResized;
    }

    public void PresentTexture(Texture2D? texture)
    {
        _texture = texture;
        QueueRedraw();
    }

    public void AttachNativeSurface(Control? surface)
    {
        if (surface is null || surface == _native)
        {
            return;
        }
        if (_native is not null && IsInstanceValid(_native))
        {
            _native.QueueFree();
        }
        _native = surface;
        AddChild(_native);
        _native.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        OnResized();
    }

    public bool HasNativeSurface => _native is not null && IsInstanceValid(_native);

    public bool IsLive => _live;

    public void SetLive(bool value)
    {
        if (_live != value)
        {
            _live = value;
            QueueRedraw();
        }
    }

    public void SetReversed(bool value)
    {
        if (_reverse != value)
        {
            _reverse = value;
            OnResized();
            QueueRedraw();
        }
    }

    public void SetSignalPattern(bool value)
    {
        if (_signalPattern != value)
        {
            _signalPattern = value;
            QueueRedraw();
        }
    }

    private void OnResized()
    {
        if (_native is not null && IsInstanceValid(_native))
        {
            _native.PivotOffset = Size * 0.5f;
            _native.Scale = _reverse ? new Vector2(-1, -1) : Vector2.One;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        if (_live)
        {
            DrawRect(bounds, Colors.Black);
            return;
        }

        if (_texture is not null && _texture.GetWidth() > 0 && _texture.GetHeight() > 0)
        {
            DrawRect(bounds, UiKit.Bg);
            var texSize = _texture.GetSize();
            var coverScale = MathF.Max(Size.X / texSize.X, Size.Y / texSize.Y);
            var displaySize = texSize * coverScale;
            var target = new Rect2((Size - displaySize) * 0.5f, displaySize);
            if (_reverse)
            {
                target.Position += target.Size;
                target.Size = -target.Size;
            }
            DrawTextureRect(_texture, target, false);
            return;
        }

        var horizon = Size.Y * 0.49f;
        DrawPolygon(
            new[] { new Vector2(0, 0), new Vector2(Size.X, 0), new Vector2(Size.X, horizon), new Vector2(0, horizon) },
            new[] { new Color("23272f"), new Color("23272f"), new Color("15181e"), new Color("15181e") });
        DrawPolygon(
            new[] { new Vector2(0, horizon), new Vector2(Size.X, horizon), new Vector2(Size.X, Size.Y), new Vector2(0, Size.Y) },
            new[] { new Color("0b0d11"), new Color("0b0d11"), new Color("161a21"), new Color("161a21") });

        if (_signalPattern)
        {
            var grid = new Color(UiKit.Accent, 0.05f);
            for (var x = 0; x <= (int)Size.X; x += 96)
            {
                DrawLine(new Vector2(x, horizon), new Vector2(x, Size.Y), grid);
            }
            for (var y = (int)horizon; y <= (int)Size.Y; y += 72)
            {
                DrawLine(new Vector2(0, y), new Vector2(Size.X, y), grid);
            }
            const string caption = "图传画面占位 · FPV FEED PLACEHOLDER";
            var captionSize = UiKit.Font().GetStringSize(caption, HorizontalAlignment.Left, -1, 15);
            DrawString(UiKit.Font(), new Vector2((Size.X - captionSize.X) * 0.5f, Size.Y - 14f), caption, HorizontalAlignment.Left, -1, 15, UiKit.Dim);
        }

        var shade = new Color(0, 0, 0, 0.45f);
        var clear = new Color(0, 0, 0, 0f);
        var band = MathF.Min(Size.X, Size.Y) * 0.22f;
        DrawPolygon(
            new[] { new Vector2(0, 0), new Vector2(band, 0), new Vector2(band, Size.Y), new Vector2(0, Size.Y) },
            new[] { shade, clear, clear, shade });
        DrawPolygon(
            new[] { new Vector2(Size.X - band, 0), new Vector2(Size.X, 0), new Vector2(Size.X, Size.Y), new Vector2(Size.X - band, Size.Y) },
            new[] { clear, shade, shade, clear });
        DrawPolygon(
            new[] { new Vector2(0, Size.Y - band), new Vector2(Size.X, Size.Y - band), new Vector2(Size.X, Size.Y), new Vector2(0, Size.Y) },
            new[] { clear, clear, shade, shade });
    }
}
