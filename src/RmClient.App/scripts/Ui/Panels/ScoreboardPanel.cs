using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.App.Ui.Components;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class ScoreboardPanel : TerminalPanel
{
    private static readonly float[] ColumnX = { 14f, 48f, 238f, 328f, 416f, 512f };
    private static readonly float[] ColumnWidth = { 30f, 180f, 84f, 84f, 90f, 82f };

    private Label _summary = null!;
    private readonly List<Label> _totals = new();
    private readonly List<ScoreboardRow> _rows = new();

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<MatchClockUpdatedEvent>();
        SubscribeRefresh<BaseHealthUpdatedEvent>();
        SubscribeRefresh<RobotHealthUpdatedEvent>();
        SubscribeRefresh<RobotHeatPowerUpdatedEvent>();
        SubscribeRefresh<RobotAmmoUpdatedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        _summary = TextAt(Body, "总伤害  —  :  —", new Rect2(30f, 58f, 1240f, 38f), 24, UiKit.Cyan, HorizontalAlignment.Center);
        string[] headers = { "#", "机器人信息", "功率 W", "热量", "冷却", "初速 m/s" };

        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var section = Body.GetNode<Control>(sideIndex == 0 ? "RedTeam" : "BlueTeam");
            var color = sideIndex == 0 ? UiKit.Red : UiKit.Blue;
            _totals.Add(TextAt(section, string.Empty, new Rect2(12f, 6f, 590f, 32f), 18, color));
            for (var col = 0; col < headers.Length; col++)
            {
                TextAt(section, headers[col], new Rect2(ColumnX[col], 43f, ColumnWidth[col], 28f), 15, UiKit.Muted, HorizontalAlignment.Center);
            }
            UiKit.CreateBar(section, new Rect2(12f, 75f, 590f, 3f), 100, color);

            for (var i = 0; i < 6; i++)
            {
                var y = 84f + i * 65f;
                var row = UiKit.CreatePanel(section, new Rect2(10f, y, 595f, 59f), UiKit.PanelSoft);
                var portrait = new TextureRect
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                row.AddChild(portrait);
                UiKit.SetRect(portrait, 32f, 8f, 62f, 38f);

                _rows.Add(new ScoreboardRow(
                    Id: TextAt(row, string.Empty, new Rect2(2f, 8f, 28f, 40f), 19, color, HorizontalAlignment.Center),
                    Portrait: portrait,
                    Title: TextAt(row, string.Empty, new Rect2(100f, 1f, 120f, 25f), 15),
                    Health: TextAt(row, string.Empty, new Rect2(100f, 32f, 120f, 21f), 12, UiKit.Text, HorizontalAlignment.Center),
                    Bar: UiKit.CreateBar(row, new Rect2(100f, 27f, 120f, 4f), 100, color),
                    Power: TextAt(row, string.Empty, new Rect2(228f, 12f, 84f, 32f), 17, UiKit.Text, HorizontalAlignment.Center),
                    Heat: TextAt(row, string.Empty, new Rect2(318f, 12f, 84f, 32f), 17, UiKit.Text, HorizontalAlignment.Center),
                    Cooldown: TextAt(row, "—", new Rect2(406f, 12f, 90f, 32f), 17, UiKit.Muted, HorizontalAlignment.Center),
                    Speed: TextAt(row, string.Empty, new Rect2(502f, 12f, 82f, 32f), 17, UiKit.Text, HorizontalAlignment.Center)));
            }
        }

        TextAt(Body, "总伤害 / 冷却 / 飞镖命中未提供遥测，显示 —；功率与热量为当前值 / 上限。", new Rect2(30f, 620f, 1240f, 28f), 16, UiKit.Muted);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        var state = Store.State;
        _summary.Text = $"总伤害  —  :  —     |     第 {state.RoundIndex} 局   {FormatClock(state.RemainingSeconds)}";

        for (var sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            var side = sideIndex == 0 ? TeamSide.Red : TeamSide.Blue;
            var team = state.GetTeam(side);
            _totals[sideIndex].Text = $"{team.Title}     机器人剩余血量 {team.TotalHealth()}     基地 {team.BaseHealth}";

            for (var i = 0; i < 6 && i < team.Robots.Count; i++)
            {
                var row = _rows[sideIndex * 6 + i];
                var robot = team.Robots[i];
                row.Portrait.Texture = RobotCard.GetScoreboardPortrait(robot.Id, side);
                row.Id.Text = robot.Id.ToString();
                row.Title.Text = $"{robot.Title}  Lv{robot.Level}";
                row.Health.Text = $"{robot.Health} / {robot.MaxHealth}";
                row.Bar.MaxValue = Math.Max(robot.MaxHealth, 1);
                row.Bar.Value = robot.Health;
                row.Power.Text = $"{robot.Power:F0}/{robot.PowerLimit:F0}";
                row.Heat.Text = $"{robot.Heat:F0}/{robot.HeatLimit:F0}";
                row.Speed.Text = $"{robot.MuzzleSpeed:F1}";
            }
        }
    }

    private sealed record ScoreboardRow(
        Label Id,
        TextureRect Portrait,
        Label Title,
        Label Health,
        ProgressBar Bar,
        Label Power,
        Label Heat,
        Label Cooldown,
        Label Speed);
}
