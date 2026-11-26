using System;
using System.Collections.Generic;
using Godot;

namespace RmClient.App.Theme;

/// <summary>
/// Lightweight combat-first UI micro-feedback: zero entry delay, single short tween per channel,
/// immediate cancellation on rapid input, and automatic opt-out in headless or --no-ui-motion mode.
/// </summary>
public static class UiMotion
{
    public const float ValueTime = 0.06f;
    public const float FeedbackTime = 0.06f;
    private const string FeedbackMeta = "_ui_motion_button";

    private static readonly Dictionary<ulong, Dictionary<string, WeakReference<Tween>>> ActiveTweens = new();

    public static bool IsEnabled()
    {
        if (Engine.IsEditorHint() || DisplayServer.GetName() == "headless")
        {
            return false;
        }
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--no-ui-motion")
            {
                return false;
            }
        }
        return true;
    }

    public static Tween? Animate(Node target, NodePath property, Variant value, float duration = ValueTime, string channel = "value")
    {
        Cancel(target, channel);
        if (!IsEnabled() || !target.IsInsideTree() || target.GetIndexed(property).Equals(value))
        {
            target.SetIndexed(property, value);
            return null;
        }

        var tween = target.CreateTween();
        tween.SetIgnoreTimeScale(true);
        tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(target, property, value, duration);

        var targetId = target.GetInstanceId();
        if (!ActiveTweens.TryGetValue(targetId, out var channels))
        {
            channels = new Dictionary<string, WeakReference<Tween>>(StringComparer.Ordinal);
            ActiveTweens[targetId] = channels;
        }
        var tweenRef = new WeakReference<Tween>(tween);
        channels[channel] = tweenRef;

        tween.Finished += () => ReleaseChannel(targetId, tweenRef, channel);
        return tween;
    }

    public static void Cancel(Node target, string channel)
    {
        var targetId = target.GetInstanceId();
        if (!ActiveTweens.TryGetValue(targetId, out var channels))
        {
            return;
        }
        if (channels.TryGetValue(channel, out var tweenRef)
            && tweenRef.TryGetTarget(out var tween)
            && GodotObject.IsInstanceValid(tween)
            && tween.IsValid())
        {
            tween.Kill();
        }
        channels.Remove(channel);
        if (channels.Count == 0)
        {
            ActiveTweens.Remove(targetId);
        }
    }

    public static int ActiveCount(Node target)
    {
        var targetId = target.GetInstanceId();
        if (!ActiveTweens.TryGetValue(targetId, out var channels))
        {
            return 0;
        }
        var count = 0;
        foreach (var entry in channels.Values)
        {
            if (entry.TryGetTarget(out var tween)
                && GodotObject.IsInstanceValid(tween)
                && tween.IsValid()
                && tween.IsRunning())
            {
                count++;
            }
        }
        return count;
    }

    public static void Present(Control control)
    {
        var mod = control.Modulate;
        mod.A = 1f;
        control.Modulate = mod;
        control.Show();
    }

    public static void Dismiss(Control control)
    {
        control.Hide();
        var mod = control.Modulate;
        mod.A = 1f;
        control.Modulate = mod;
    }

    public static void Pulse(Control control)
    {
        if (!IsEnabled() || !control.IsVisibleInTree())
        {
            return;
        }
        control.SelfModulate = new Color(1.10f, 1.10f, 1.10f, 1.0f);
        Animate(control, new NodePath("self_modulate"), Variant.From(Colors.White), FeedbackTime, "feedback");
    }

    public static void AttachButtons(Node root)
    {
        if (!IsEnabled())
        {
            return;
        }
        if (root is BaseButton baseButton
            && !baseButton.HasMeta(FeedbackMeta)
            && !(baseButton is Button btn && btn.Flat))
        {
            var feedback = new ButtonFeedback { Name = "MotionFeedback", TargetButton = baseButton };
            baseButton.SetMeta(FeedbackMeta, true);
            baseButton.AddChild(feedback);
        }

        foreach (var child in root.GetChildren())
        {
            if (child is not ButtonFeedback)
            {
                AttachButtons(child);
            }
        }
    }

    private static void ReleaseChannel(ulong targetId, WeakReference<Tween> tweenRef, string channel)
    {
        if (!ActiveTweens.TryGetValue(targetId, out var channels))
        {
            return;
        }
        if (channels.TryGetValue(channel, out var current) && ReferenceEquals(current, tweenRef))
        {
            channels.Remove(channel);
        }
        if (channels.Count == 0)
        {
            ActiveTweens.Remove(targetId);
        }
    }
}

public partial class ButtonFeedback : Node
{
    public BaseButton? TargetButton;

    private StyleBoxFlat? _style;
    private Color _normal;
    private Color _normalEdge;
    private Color _hover;
    private Color _hoverEdge;
    private Color _pressed;
    private Color _pressedEdge;
    private Color _from;
    private Color _fromEdge;
    private Color _to;
    private Color _toEdge;
    private bool _hovered;
    private bool _down;
    private float _blend = 1f;

    public float Blend
    {
        get => _blend;
        set
        {
            _blend = value;
            if (_style is not null)
            {
                _style.BgColor = _from.Lerp(_to, value);
                _style.BorderColor = _fromEdge.Lerp(_toEdge, value);
            }
        }
    }

    public override void _Ready()
    {
        if (TargetButton is null)
        {
            return;
        }
        var original = TargetButton.GetThemeStylebox("normal");
        if (original is not StyleBoxFlat flatOriginal || (TargetButton is Button btn && btn.Flat))
        {
            return;
        }

        _style = flatOriginal.Duplicate() as StyleBoxFlat;
        if (_style is null)
        {
            return;
        }
        _normal = _style.BgColor;
        _normalEdge = _style.BorderColor;

        var hover = TargetButton.GetThemeStylebox("hover") as StyleBoxFlat;
        var pressed = TargetButton.GetThemeStylebox("pressed") as StyleBoxFlat;
        _hover = hover?.BgColor ?? _normal.Lightened(0.12f);
        _hoverEdge = hover?.BorderColor ?? _normalEdge;
        _pressed = pressed?.BgColor ?? _hover;
        _pressedEdge = pressed?.BorderColor ?? _hoverEdge;

        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
        {
            TargetButton.AddThemeStyleboxOverride(state, _style);
        }

        TargetButton.MouseEntered += () => { _hovered = true; UpdateState(); };
        TargetButton.MouseExited += () => { _hovered = false; UpdateState(); };
        TargetButton.ButtonDown += () => { _down = true; UpdateState(); };
        TargetButton.ButtonUp += () => { _down = false; UpdateState(); };
        TargetButton.FocusEntered += UpdateState;
        TargetButton.FocusExited += UpdateState;
        TargetButton.Toggled += _ => UpdateState();
        TargetButton.VisibilityChanged += OnVisibilityChanged;
    }

    private void UpdateState()
    {
        if (_style is null || TargetButton is null)
        {
            return;
        }
        UiMotion.Cancel(this, "state");
        _from = _style.BgColor;
        _fromEdge = _style.BorderColor;
        var down = _down || (TargetButton.ToggleMode && TargetButton.ButtonPressed);
        var hover = _hovered || TargetButton.HasFocus();
        _to = down ? _pressed : (hover ? _hover : _normal);
        _toEdge = down ? _pressedEdge : (hover ? _hoverEdge : _normalEdge);
        Blend = 0f;
        UiMotion.Animate(this, new NodePath(nameof(Blend)), Variant.From(1f), UiMotion.FeedbackTime, "state");
    }

    private void OnVisibilityChanged()
    {
        if (TargetButton is null || TargetButton.IsVisibleInTree())
        {
            return;
        }
        UiMotion.Cancel(this, "state");
        _hovered = false;
        _down = false;
        _to = _normal;
        _toEdge = _normalEdge;
        Blend = 1f;
    }
}
