using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Commands;
using RmClient.Core.Events;
using RmClient.Core.Events.Commands;
using RmClient.Core.Settings;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

/// <summary>
/// Shared lifecycle and modal confirmation helper for all terminal panels.
/// Keyboard ownership stays with AppRoot.
/// </summary>
public abstract partial class TerminalPanel : Panel
{
    public event Action? CloseRequested;

    protected TerminalStore? Store;
    protected TerminalCommands? Commands;
    protected UserSettingsService? Settings;

    private readonly List<IDisposable> _subscriptions = new();
    private bool _built;
    private Control? _confirmation;
    private Action? _confirmAction;
    private bool _feedbackReceived;
    private bool _active;
    private bool _dirty = true;
    private bool _motionReady;

    public Control Body { get; private set; } = null!;
    public Label Footer { get; private set; } = null!;

    public override void _Ready()
    {
        Body = GetNode<Control>("Body");
        Footer = GetNode<Label>("Footer");

        AddThemeStyleboxOverride("panel", UiKit.CreateStyle(new Color(0.031f, 0.047f, 0.071f, 0.985f), UiKit.Line));
        AddThemeFontOverride("font", UiKit.Font());
        Resized += QueueRedraw;

        Body.MouseFilter = MouseFilterEnum.Ignore;
        Body.ClipContents = true;
        foreach (var child in Body.GetChildren())
        {
            if (child is Control control)
            {
                control.ClipContents = true;
            }
        }

        var closeButton = GetNode<Button>("Close");
        closeButton.FocusMode = FocusModeEnum.All;
        closeButton.Pressed += RequestClose;

        BuildContent();
        _built = true;
        RefreshIfReady();
    }

    public override void _ExitTree()
    {
        ClearSubscriptions();
    }

    public virtual void Bind(TerminalStore store, TerminalCommands commands, UserSettingsService settings)
    {
        ClearSubscriptions();
        CancelConfirmation();
        Store = store;
        Commands = commands;
        Settings = settings;

        if (Store is not null)
        {
            Subscribe<CommandFeedbackEvent>(OnCommandFeedback);
            RegisterEventSubscriptions();
        }
        RefreshIfReady();
    }

    protected virtual void RegisterEventSubscriptions()
    {
    }

    protected void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
    {
        if (Store is null)
        {
            return;
        }
        _subscriptions.Add(Store.Bus.Subscribe(handler));
    }

    protected void SubscribeRefresh<TEvent>() where TEvent : IDomainEvent
    {
        Subscribe<TEvent>(_ => RefreshIfReady());
    }

    private void ClearSubscriptions()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }

    public virtual void SetActive(bool active)
    {
        var wasActive = _active;
        _active = active;
        if (!active)
        {
            CancelConfirmation();
            UiMotion.Dismiss(this);
            return;
        }

        Show();
        if (_built && (_dirty || !wasActive))
        {
            RefreshIfReady();
        }
        if (!wasActive)
        {
            UiMotion.Present(this);
            if (!_motionReady)
            {
                UiMotion.AttachButtons(this);
                _motionReady = true;
            }
        }
    }

    public virtual void Refresh()
    {
    }

    public override void _Draw()
    {
        UiKit.DrawBrackets(this, new Rect2(new Vector2(-5f, -5f), Size + new Vector2(10f, 10f)), new Color(UiKit.Accent, 0.7f), 18f, 2f);
        DrawRect(new Rect2(0f, 0f, 64f, 3f), UiKit.Accent);
        DrawRect(new Rect2(8f, 18f, 3f, 26f), new Color(UiKit.Accent, 0.85f));
        DrawLine(new Vector2(20f, 58f), new Vector2(Size.X - 20f, 58f), UiKit.Line, 1f);
    }

    protected virtual void BuildContent()
    {
    }

    protected void RefreshIfReady()
    {
        _dirty = true;
        if (_active && IsVisibleInTree() && _built && Store is not null)
        {
            Refresh();
            _dirty = false;
        }
    }

    protected virtual void RequestClose()
    {
        CloseRequested?.Invoke();
    }

    protected static Label TextAt(
        Node parent,
        string text,
        Rect2 bounds,
        int fontSize = 18,
        Color? color = null,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = UiKit.CreateLabel(parent, text, bounds, fontSize, color, align);
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.Size = bounds.Size;
        return label;
    }

    protected static Button ButtonAt(Node parent, string text, Rect2 bounds, Action callback)
    {
        var result = UiKit.CreateButton(parent, text, bounds, callback);
        result.FocusMode = FocusModeEnum.All;
        return result;
    }

    protected static OptionButton ChoiceAt(Node parent, IReadOnlyList<string> items, Rect2 bounds, Action<long>? callback = null)
    {
        var result = UiKit.CreateChoice(parent, items, bounds, callback);
        result.FocusMode = FocusModeEnum.All;
        return result;
    }

    public void SetFeedback(string message, bool isError = false)
    {
        if (!_built)
        {
            return;
        }
        Footer.Text = message;
        Footer.AddThemeColorOverride("font_color", isError ? UiKit.Red : UiKit.Cyan);
    }

    protected bool RunCommand(Func<TerminalCommands, bool> invocation)
    {
        if (Commands is null)
        {
            SetFeedback("命令接口未绑定，未执行。", isError: true);
            return false;
        }
        _feedbackReceived = false;
        var accepted = invocation(Commands);
        if (!_feedbackReceived)
        {
            SetFeedback(accepted ? "本地演示指令已执行。" : "指令被拒绝，未执行。", isError: !accepted);
        }
        else if (!accepted)
        {
            Footer.AddThemeColorOverride("font_color", UiKit.Red);
        }
        return accepted;
    }

    private void OnCommandFeedback(CommandFeedbackEvent e)
    {
        if (IsVisibleInTree())
        {
            _feedbackReceived = true;
            SetFeedback(e.Message, isError: !e.Accepted);
        }
    }

    protected void AskConfirmation(string message, Action action)
    {
        CancelConfirmation();
        _confirmAction = action;
        var blocker = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.045f, 0.82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(blocker);
        blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _confirmation = blocker;

        var boxWidth = MathF.Min(Size.X - 48f, 640f);
        var box = UiKit.CreatePanel(
            blocker,
            new Rect2((Size.X - boxWidth) * 0.5f, (Size.Y - 234f) * 0.5f, boxWidth, 234f),
            new Color(0.04f, 0.055f, 0.08f, 1f));

        var stripe = new ColorRect
        {
            Color = UiKit.Accent,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        box.AddChild(stripe);
        UiKit.SetRect(stripe, 0f, 0f, boxWidth, 3f);

        TextAt(box, "二次确认 · 本地演示", new Rect2(20f, 12f, boxWidth - 40f, 32f), 22, UiKit.Accent);
        var prompt = TextAt(box, message, new Rect2(20f, 52f, boxWidth - 40f, 106f), 19);
        prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        ButtonAt(box, "取消", new Rect2(20f, 174f, (boxWidth - 52f) * 0.5f, 40f), CancelConfirmation);
        var confirm = ButtonAt(box, "确认兑换", new Rect2((boxWidth + 12f) * 0.5f, 174f, (boxWidth - 52f) * 0.5f, 40f), Confirm);
        UiMotion.AttachButtons(blocker);
        UiMotion.Present(box);
        confirm.GrabFocus();
    }

    public void CancelConfirmation()
    {
        _confirmAction = null;
        if (_confirmation is not null && IsInstanceValid(_confirmation))
        {
            _confirmation.Hide();
            _confirmation.QueueFree();
        }
        _confirmation = null;
    }

    private void Confirm()
    {
        var action = _confirmAction;
        CancelConfirmation();
        action?.Invoke();
    }

    protected static string FormatClock(int seconds)
    {
        var safe = Math.Max(seconds, 0);
        return $"{safe / 60}:{safe % 60:D2}";
    }
}
