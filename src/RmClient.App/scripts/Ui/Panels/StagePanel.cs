using Godot;
using RmClient.App.Theme;
using RmClient.Core.Events.Match;

namespace RmClient.App.Ui.Panels;

public partial class StagePanel : TerminalPanel
{
    [Export]
    public StringName ActionName { get; set; } = new("rune");

    private Label _stage = null!;
    private Label _example = null!;

    protected override void RegisterEventSubscriptions()
    {
        SubscribeRefresh<MatchClockUpdatedEvent>();
        SubscribeRefresh<DemoStateResetEvent>();
    }

    protected override void BuildContent()
    {
        var section = Body.GetNode<Control>("Stage");
        _stage = TextAt(section, "等待比赛数据", new Rect2(20f, 12f, 672f, 36f), 23, UiKit.Cyan);
        _example = TextAt(section, string.Empty, new Rect2(20f, 58f, 672f, 100f), 19);
        _example.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var buttonText = ActionName == "rune" ? "记录能量机关演示" : "记录部署演示";
        ButtonAt(section, buttonText, new Rect2(20f, 174f, 672f, 44f), ExecuteDemo);
    }

    public override void Refresh()
    {
        if (Store is null)
        {
            return;
        }
        var state = Store.State;
        var phase = state.RemainingSeconds > 0 ? "倒计时阶段" : "已结束";
        _stage.Text = $"第 {state.RoundIndex} 局 · {phase} · {FormatClock(state.RemainingSeconds)}";
        _example.Text = (ActionName == "rune"
            ? "示例说明：能量机关请求仅记录本地事件，不代表命中、激活或真实设备响应。"
            : "示例说明：部署请求仅记录本地事件；未连接英雄机器人，不会改变真实部署状态。")
            + "\n精确赛事阶段 / 设备就绪状态未由当前快照提供。";
    }

    private void ExecuteDemo()
    {
        if (ActionName == "rune")
        {
            RunCommand(c => c.TriggerRune());
        }
        else
        {
            RunCommand(c => c.TriggerDeployment());
        }
    }
}
