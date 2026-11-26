using System;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Commands;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class SupplyPanel : TerminalPanel
{
    [Export]
    public int Caliber { get; set; } = 17;

    private int _amount;
    private Label _summary = null!;
    private Label _selection = null!;

    public int PendingAmount => _amount;

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<ActiveRobotChangedEvent>();
        SubscribeRefresh<CreditsUpdatedEvent>();
        SubscribeRefresh<RobotAmmoUpdatedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        var section = Body.GetNode<Control>("Supply");
        _summary = TextAt(section, string.Empty, new Rect2(20f, 12f, 780f, 68f), 21, UiKit.Text, HorizontalAlignment.Center);
        _selection = TextAt(section, string.Empty, new Rect2(20f, 84f, 780f, 36f), 22, UiKit.Cyan, HorizontalAlignment.Center);

        int[] increments = { 20, 50, 100, 200 };
        for (var i = 0; i < increments.Length; i++)
        {
            var inc = increments[i];
            ButtonAt(section, $"+{inc}", new Rect2(20f + i * 158f, 136f, 148f, 44f), () => Increase(inc));
        }
        ButtonAt(section, "清零", new Rect2(652f, 136f, 148f, 44f), ClearAmount);
        ButtonAt(section, "兑换 · 需确认", new Rect2(200f, 198f, 420f, 42f), RequestPurchase);
    }

    public override void SetActive(bool active)
    {
        if (!active)
        {
            _amount = 0;
        }
        base.SetActive(active);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        var robot = Store.State.GetActiveRobot();
        var ammo = robot?.Ammo ?? 0;
        var cost = Commands is not null && _amount > 0 ? TerminalCommands.GetPurchaseCost(_amount, Caliber) : 0;
        _summary.Text = $"已有允许发弹量 {ammo} / 1000    |    余额 {Store.State.Credits} 金币\n{Caliber} mm 补弹 · 仅本地演示，不校验真实补给区 RFID";
        _selection.Text = $"本次 +{_amount} 发    预计 {cost} 金币    （单次上限 500）";
    }

    private void Increase(int increment)
    {
        _amount = Math.Min(_amount + increment, TerminalCommands.MaxPurchaseAmount);
        RefreshIfReady();
    }

    private void ClearAmount()
    {
        _amount = 0;
        CancelConfirmation();
        RefreshIfReady();
    }

    private bool Validate(int amount, int robotId, TeamSide team)
    {
        if (Store is null || Commands is null)
        {
            SetFeedback("接口未绑定，无法兑换。", isError: true);
            return false;
        }
        var robot = Store.State.GetActiveRobot();
        if (robot is null || robot.Id != robotId || Store.State.ActiveTeam != team)
        {
            SetFeedback("登录机器人已改变，请重新确认。", isError: true);
            return false;
        }
        var cost = TerminalCommands.GetPurchaseCost(amount, Caliber);
        if (cost < 0 || amount <= 0)
        {
            SetFeedback("请先选择 1–500 发弹量。", isError: true);
            return false;
        }
        if (cost > Store.State.Credits)
        {
            SetFeedback("余额不足，兑换已拒绝。", isError: true);
            return false;
        }
        if (robot.Ammo + amount > TerminalCommands.MaxAmmo)
        {
            SetFeedback("兑换后弹量超过 1000，请清零后重新选择。", isError: true);
            return false;
        }
        return true;
    }

    private void RequestPurchase()
    {
        if (Store is null)
        {
            return;
        }
        var robot = Store.State.GetActiveRobot();
        var activeTeam = Store.State.ActiveTeam;
        if (robot is null || !Validate(_amount, robot.Id, activeTeam))
        {
            return;
        }
        var amount = _amount;
        var robotId = robot.Id;
        var cost = TerminalCommands.GetPurchaseCost(amount, Caliber);
        AskConfirmation(
            $"向 {robot.Id}号 {robot.Title} 补给 {amount} 发 {Caliber} mm 弹丸？\n消耗 {cost} 金币，当前余额 {Store.State.Credits}。仅修改本地演示。",
            () => ExecutePurchase(amount, robotId, activeTeam));
    }

    private void ExecutePurchase(int amount, int robotId, TeamSide team)
    {
        if (!Validate(amount, robotId, team))
        {
            return;
        }
        if (RunCommand(c => c.PurchaseAmmo(amount, Caliber)))
        {
            _amount = 0;
        }
        RefreshIfReady();
    }
}
