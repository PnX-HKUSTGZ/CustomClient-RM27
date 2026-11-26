using System.Collections.Generic;
using Godot;
using RmClient.App.Theme;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

public partial class ModulesPanel : TerminalPanel
{
    private Label _moduleList = null!;
    private Label _robot = null!;

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<ModuleStatusUpdatedEvent>();
        SubscribeRefresh<ActiveRobotChangedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        var damage = Body.GetNode<Control>("Damage");
        TextAt(damage, "伤害统计", new Rect2(14f, 8f, 370f, 32f), 21);
        TextAt(damage, "17 mm 弹丸          —   |   —\n42 mm 弹丸          —   |   —\n撞击 / 模块离线     —   |   —\n警告                     —   |   —", new Rect2(14f, 48f, 370f, 130f), 19);
        TextAt(damage, "快照未提供伤害类型 / 数值 / 占比", new Rect2(14f, 184f, 370f, 24f), 15, UiKit.Muted);

        var section = Body.GetNode<Control>("ModuleGrid");
        TextAt(section, "模块状态", new Rect2(14f, 8f, 370f, 32f), 21);
        _moduleList = TextAt(section, string.Empty, new Rect2(14f, 50f, 370f, 174f), 18);
        _robot = TextAt(Body, string.Empty, new Rect2(18f, 564f, 392f, 36f), 17, UiKit.Cyan);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        var lines = new List<string>();
        foreach (var pair in Store.State.Modules)
        {
            var label = pair.Key switch
            {
                "chassis" => "底盘",
                "gimbal" => "云台",
                "shooter" => "发射机构",
                "referee" => "裁判系统",
                "video" => "图传",
                "custom_terminal" => "自定义终端",
                "air_support" => "空中支援（演示）",
                _ => pair.Key,
            };
            lines.Add($"{label}   {(pair.Value ? "正常" : "离线 / 未连接")}");
        }

        _moduleList.Text = string.Join("\n", lines);
        _moduleList.AddThemeFontSizeOverride("font_size", lines.Count > 7 ? 15 : 18);
        _moduleList.VerticalAlignment = VerticalAlignment.Top;

        var robot = Store.State.GetActiveRobot();
        _robot.Text = robot is not null
            ? $"{Store.State.ActiveTeam.ToCode()} · {robot.Id}号 {robot.Title}"
            : "尚未选择机器人";
    }
}
