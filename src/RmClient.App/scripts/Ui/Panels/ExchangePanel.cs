using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Commands;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class ExchangePanel : TerminalPanel
{
    private static readonly RemoteExchangeKind[] Kinds =
    {
        RemoteExchangeKind.Ammo,
        RemoteExchangeKind.Health,
        RemoteExchangeKind.AirSupport,
        RemoteExchangeKind.Energy,
    };

    private static readonly string[] Titles = { "弹量 +50", "血量 +100", "空中支援", "底盘能量 +25" };

    private Label _balance = null!;
    private readonly List<Button> _buttons = new();

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<ActiveRobotChangedEvent>();
        SubscribeRefresh<CreditsUpdatedEvent>();
        SubscribeRefresh<ModuleStatusUpdatedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        var section = Body.GetNode<Control>("Exchange");
        _balance = TextAt(section, string.Empty, new Rect2(20f, 6f, 832f, 40f), 23, UiKit.Cyan);

        for (var i = 0; i < Kinds.Length; i++)
        {
            var col = i % 2;
            var row = i / 2;
            var kind = Kinds[i];
            var title = Titles[i];
            _buttons.Add(ButtonAt(section, title, new Rect2(20f + col * 428f, 64f + row * 96f, 404f, 74f), () => RequestExchange(kind, title)));
        }

        TextAt(section, "仅本地演示；血量 / 能量受上限约束，最终结果以命令反馈为准。", new Rect2(20f, 272f, 832f, 44f), 17, UiKit.Muted);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        _balance.Text = $"当前余额 {Store.State.Credits} 金币 · 远程兑换需二次确认";
        for (var i = 0; i < Kinds.Length; i++)
        {
            var cost = Commands is not null ? TerminalCommands.GetRemoteCost(Kinds[i]) : -1;
            _buttons[i].Text = $"{Titles[i]}    {cost} 金币";
        }
    }

    private void RequestExchange(RemoteExchangeKind kind, string title)
    {
        if (Store is null || Commands is null)
        {
            SetFeedback("接口未绑定。", isError: true);
            return;
        }
        var cost = TerminalCommands.GetRemoteCost(kind);
        var robot = Store.State.GetActiveRobot();
        if (cost < 0 || robot is null)
        {
            SetFeedback("无效兑换目标。", isError: true);
            return;
        }
        if (cost > Store.State.Credits)
        {
            SetFeedback("余额不足，兑换已拒绝。", isError: true);
            return;
        }
        var robotId = robot.Id;
        var activeTeam = Store.State.ActiveTeam;
        AskConfirmation(
            $"为 {robot.Id}号 {robot.Title} 兑换 {title}？\n消耗 {cost} 金币。此操作仅修改本地演示数据。",
            () => ExecuteExchange(kind, robotId, activeTeam));
    }

    private void ExecuteExchange(RemoteExchangeKind kind, int robotId, TeamSide team)
    {
        if (Store is null || Commands is null)
        {
            return;
        }
        var robot = Store.State.GetActiveRobot();
        if (robot is null || robot.Id != robotId || Store.State.ActiveTeam != team)
        {
            SetFeedback("登录已改变，请重新确认。", isError: true);
            return;
        }
        var cost = TerminalCommands.GetRemoteCost(kind);
        if (cost < 0 || cost > Store.State.Credits)
        {
            SetFeedback("余额不足或兑换无效，未执行。", isError: true);
            return;
        }
        RunCommand(c => c.RemoteExchange(kind));
    }
}
