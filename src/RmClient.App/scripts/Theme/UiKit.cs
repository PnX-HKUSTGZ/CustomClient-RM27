using System;
using System.Collections.Generic;
using Godot;

namespace RmClient.App.Theme;

/// <summary>
/// Unified palette, typography, and programmatic control factory for HUD and modal panels.
/// Desaturated slate tactical palette with a single muted amber accent.
/// </summary>
public static class UiKit
{
    public static readonly Color Text = new("e2e6eb");
    public static readonly Color Muted = new("97a1ad");
    public static readonly Color Dim = new("67717f");
    public static readonly Color Accent = new("c48a52");
    public static readonly Color Cyan = new("7db6c2");
    public static readonly Color Red = new("cc6a60");
    public static readonly Color Blue = new("6199cc");
    public static readonly Color Green = new("6bbd97");
    public static readonly Color Gold = new("cfa253");
    public static readonly Color Bg = new("0a0d13");
    public static readonly Color Panel = new(0.031f, 0.047f, 0.071f, 0.86f);
    public static readonly Color PanelSoft = new(0.06f, 0.08f, 0.11f, 0.62f);
    public static readonly Color Line = new(0.78f, 0.83f, 0.89f, 0.16f);
    public static readonly Color LineStrong = new(0.78f, 0.83f, 0.89f, 0.34f);
    public static readonly Color Track = new(1f, 1f, 1f, 0.12f);

    private static SystemFont? _font;

    public static SystemFont Font()
    {
        if (_font is null)
        {
            _font = new SystemFont
            {
                FontNames = new[] { "Bahnschrift", "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "sans-serif" },
                FontWeight = 600,
                AllowSystemFallback = true,
            };
        }
        return _font;
    }

    public static Control SetRect(Control node, float x, float y, float width, float height)
    {
        node.Position = new Vector2(x, y);
        node.Size = new Vector2(width, height);
        return node;
    }

    public static StyleBoxFlat CreateStyle(Color? bg = null, Color? border = null, int radius = 0)
    {
        var result = new StyleBoxFlat
        {
            BgColor = bg ?? Panel,
            BorderColor = border ?? Line,
            AntiAliasing = false,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        result.SetBorderWidthAll(1);
        result.SetCornerRadiusAll(radius);
        return result;
    }

    public static StyleBoxFlat CreateButtonStyle(Color bg, Color rail)
    {
        var result = CreateStyle(bg, rail);
        result.SetBorderWidthAll(0);
        result.BorderWidthLeft = 3;
        return result;
    }

    public static Panel CreatePanel(Node parent, Rect2 bounds, Color? bg = null)
    {
        var node = new Panel();
        node.AddThemeStyleboxOverride("panel", CreateStyle(bg ?? Panel));
        parent.AddChild(node);
        SetRect(node, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return node;
    }

    public static Label CreateLabel(
        Node parent,
        string text,
        Rect2 bounds,
        int fontSize = 18,
        Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var node = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        node.AddThemeFontOverride("font", Font());
        node.AddThemeFontSizeOverride("font_size", fontSize);
        node.AddThemeColorOverride("font_color", color ?? Text);
        parent.AddChild(node);
        SetRect(node, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return node;
    }

    public static Button CreateButton(Node parent, string text, Rect2 bounds, Action? callback = null)
    {
        var node = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
        };
        node.AddThemeFontOverride("font", Font());
        node.AddThemeFontSizeOverride("font_size", 18);
        node.AddThemeColorOverride("font_color", Text);
        node.AddThemeColorOverride("font_hover_color", Colors.White);
        node.AddThemeColorOverride("font_disabled_color", Dim);
        node.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.09f, 0.11f, 0.145f, 0.9f), new Color(Accent, 0.45f)));
        node.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.13f, 0.15f, 0.19f, 0.95f), Accent));
        node.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(Accent.Darkened(0.6f), 0.95f), Accent));
        node.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Color(0.06f, 0.07f, 0.09f, 0.85f), new Color(Dim, 0.4f)));
        node.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        if (callback is not null)
        {
            node.Pressed += callback;
        }
        parent.AddChild(node);
        SetRect(node, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return node;
    }

    public static OptionButton CreateChoice(Node parent, IReadOnlyList<string> items, Rect2 bounds, Action<long>? callback = null)
    {
        var node = new OptionButton
        {
            FocusMode = Control.FocusModeEnum.None,
        };
        foreach (var item in items)
        {
            node.AddItem(item);
        }
        node.AddThemeFontOverride("font", Font());
        node.AddThemeFontSizeOverride("font_size", 17);
        node.AddThemeColorOverride("font_color", Text);
        node.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.07f, 0.09f, 0.12f, 0.9f), new Color(Cyan, 0.5f)));
        node.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.11f, 0.13f, 0.17f, 0.95f), Cyan));
        if (callback is not null)
        {
            node.ItemSelected += index => callback(index);
        }
        parent.AddChild(node);
        SetRect(node, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return node;
    }

    public static ProgressBar CreateBar(Node parent, Rect2 bounds, double value, Color? color = null)
    {
        var node = new ProgressBar
        {
            ShowPercentage = false,
            Value = value,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var background = CreateStyle(Track, new Color(0, 0, 0, 0), 0);
        var fill = CreateStyle(new Color(color ?? Cyan, 0.9f), new Color(0, 0, 0, 0), 0);
        foreach (var box in new[] { background, fill })
        {
            box.SetBorderWidthAll(0);
            box.ContentMarginLeft = 0;
            box.ContentMarginRight = 0;
            box.ContentMarginTop = 0;
            box.ContentMarginBottom = 0;
        }
        node.AddThemeStyleboxOverride("background", background);
        node.AddThemeStyleboxOverride("fill", fill);
        parent.AddChild(node);
        SetRect(node, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
        return node;
    }

    public static void DrawBrackets(CanvasItem canvas, Rect2 bounds, Color? color = null, float length = 12f, float width = 1.5f)
    {
        var stroke = color ?? LineStrong;
        var a = bounds.Position;
        var b = bounds.End;
        var len = MathF.Min(length, MathF.Min(bounds.Size.X, bounds.Size.Y) * 0.4f);
        canvas.DrawPolyline(new[] { a + new Vector2(0, len), a, a + new Vector2(len, 0) }, stroke, width);
        canvas.DrawPolyline(new[] { new Vector2(b.X - len, a.Y), new Vector2(b.X, a.Y), new Vector2(b.X, a.Y + len) }, stroke, width);
        canvas.DrawPolyline(new[] { new Vector2(a.X, b.Y - len), new Vector2(a.X, b.Y), new Vector2(a.X + len, b.Y) }, stroke, width);
        canvas.DrawPolyline(new[] { new Vector2(b.X - len, b.Y), b, new Vector2(b.X, b.Y - len) }, stroke, width);
    }

    public static void DrawHexBadge(CanvasItem canvas, Vector2 center, float radius, Color color, string text, int fontSize)
    {
        var points = new Vector2[6];
        var outline = new Vector2[7];
        for (var i = 0; i < 6; i++)
        {
            var angle = -MathF.PI / 2f + i * Mathf.Tau / 6f;
            var pt = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            points[i] = pt;
            outline[i] = pt;
        }
        outline[6] = points[0];
        canvas.DrawColoredPolygon(points, new Color(color.Darkened(0.55f), 0.92f));
        canvas.DrawPolyline(outline, new Color(color, 0.9f), 1.5f, true);
        var textSize = Font().GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        canvas.DrawString(Font(), center + new Vector2(-textSize.X * 0.5f, fontSize * 0.36f), text, HorizontalAlignment.Left, -1, fontSize, Text);
    }
}
