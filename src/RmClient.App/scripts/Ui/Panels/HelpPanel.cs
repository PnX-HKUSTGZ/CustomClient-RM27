using Godot;
using RmClient.App.Theme;

namespace RmClient.App.Ui.Panels;

public partial class HelpPanel : TerminalPanel
{
    protected override void BuildContent()
    {
        var section = Body.GetNode<Control>("Shortcuts");
        string[] keys =
        {
            "P", "Tab（按住）", "~（按住）", "O / I", "M", "A / B / I",
            "H", "F", "K", "F12", "Esc", "右键",
        };
        string[] descriptions =
        {
            "设置面板：登录、图传、硬件、UI",
            "双方六机器人状态与总览",
            "模块状态与伤害统计",
            "17 mm / 42 mm 补弹，二次确认",
            "展开 / 收起战术地图",
            "仅地图激活时：进攻 / 防守 / 警戒",
            "远程兑换：弹量 / 血量 / 支援 / 能量",
            "能量机关（本地演示）",
            "部署（本地演示）",
            "显示帮助，松开后关闭",
            "关闭当前面板",
            "地图内撤回最近标记（仅本地显示）",
        };

        for (var i = 0; i < keys.Length; i++)
        {
            var col = i < 6 ? 0 : 1;
            var row = i < 6 ? i : i - 6;
            var x = 16f + col * 505f;
            var y = 8f + row * 54f;
            TextAt(section, keys[i], new Rect2(x, y, 144f, 38f), 19, UiKit.Cyan);
            var desc = TextAt(section, descriptions[i], new Rect2(x + 145f, y, 335f, 42f), 16);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        TextAt(Body, "验证版：F5 重置演示   /   F6 暂停计时   /   F8 查看结算", new Rect2(24f, 438f, 1012f, 24f), 17, UiKit.Muted);
    }
}
