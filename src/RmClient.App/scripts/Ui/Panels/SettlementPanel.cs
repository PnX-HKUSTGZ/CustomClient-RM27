using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class SettlementPanel : TerminalPanel
{
    private Label _result = null!;
    private readonly List<Label> _teams = new();
    private Label _comparison = null!;

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<MatchClockUpdatedEvent>();
        SubscribeRefresh<TeamScoreUpdatedEvent>();
        SubscribeRefresh<BaseHealthUpdatedEvent>();
        SubscribeRefresh<RobotHealthUpdatedEvent>();
        SubscribeRefresh<RobotAmmoUpdatedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        _result = TextAt(Body, string.Empty, new Rect2(24f, 58f, 1372f, 48f), 28, UiKit.Cyan, HorizontalAlignment.Center);
        foreach (var side in new[] { "RedTeam", "BlueTeam" })
        {
            var section = Body.GetNode<Control>(side);
            _teams.Add(TextAt(section, string.Empty, new Rect2(18f, 12f, 414f, 488f), 20, side == "RedTeam" ? UiKit.Red : UiKit.Blue));
        }

        var comparison = Body.GetNode<Control>("Comparison");
        _comparison = TextAt(comparison, string.Empty, new Rect2(16f, 16f, 400f, 480f), 21, UiKit.Text, HorizontalAlignment.Center);
        TextAt(Body, "演示结算：显示当前快照，非官方裁定；伤害 / 击杀 / 能量机关统计未提供，统一显示 —。", new Rect2(24f, 656f, 1372f, 36f), 18, UiKit.Muted);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        var state = Store.State;
        _result.Text = $"第 {state.RoundIndex} 局   {state.Red.Title} {state.Red.Score} : {state.Blue.Score} {state.Blue.Title}   ·   快照剩余 {FormatClock(state.RemainingSeconds)}";

        for (var i = 0; i < 2; i++)
        {
            var team = i == 0 ? state.Red : state.Blue;
            var lines = new List<string> { team.Title, "机器人     血量     弹量     单兵伤害", string.Empty };
            foreach (var robot in team.Robots)
            {
                lines.Add($"{robot.Id} {robot.Title}   {robot.Health}/{robot.MaxHealth}   {robot.Ammo}   —");
                lines.Add(string.Empty);
            }
            _teams[i].Text = string.Join("\n", lines);
        }

        _comparison.Text =
            $"基地血量\n{state.Red.BaseHealth}  :  {state.Blue.BaseHealth}\n\n" +
            $"前哨站血量\n{state.Red.OutpostHealth}  :  {state.Blue.OutpostHealth}\n\n" +
            $"机器人总血量\n{state.Red.TotalHealth()}  :  {state.Blue.TotalHealth()}\n\n" +
            "伤害总量 / 击杀 / 能量机关\n—  :  —\n\n演示数据 · 不判定胜负";
    }
}
