using System.Linq;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Events.Tactical;
using RmClient.Core.State;
using Xunit;

namespace RmClient.Core.Tests;

public sealed class TerminalStoreTests
{
    [Fact]
    public void InitialStateMatchesDemoBaselineAndDoesNotShareInstances()
    {
        var store = new TerminalStore();
        var state = store.State;

        Assert.Equal(116, state.RemainingSeconds);
        Assert.Equal(1, state.RoundIndex);
        Assert.Equal(650, state.Credits);
        Assert.Equal(TeamSide.Red, state.ActiveTeam);
        Assert.Equal(3, state.ActiveRobotId);
        Assert.Equal("演示数据 · 未连接", state.ConnectionLabel);
        Assert.Equal(5000, state.Red.BaseHealth);
        Assert.Equal(5000, state.Blue.BaseHealth);
        Assert.Same(state.Red, state.GetTeam("red"));
        Assert.Same(state.Blue, state.GetTeam("blue"));
        Assert.Null(state.GetTeam("invalid"));
        Assert.Equal(225, state.GetActiveRobot()!.Health);
        Assert.Equal(4, state.Events.Count);

        foreach (var team in new[] { state.Red, state.Blue })
        {
            Assert.Equal(6, team.Robots.Count);
            Assert.Equal(DemoScenario.RobotIds, team.Robots.Select(r => r.Id).ToArray());
            foreach (var robot in team.Robots)
            {
                Assert.Equal(150, robot.Ammo);
                Assert.Equal(29.5f, robot.MuzzleSpeed, 3);
                Assert.InRange(robot.Health, 1, robot.MaxHealth);
                Assert.True(robot.Online);
                Assert.True(robot.MapPosition.IsInBounds);
            }
        }

        var other = new TerminalStore();
        state.Red.Robots[0].Ammo = 0;
        state.Modules["chassis"] = false;
        Assert.Equal(150, other.State.Red.Robots[0].Ammo);
        Assert.True(other.State.Modules["chassis"]);
    }

    [Fact]
    public void SelectionEventsAndBoundedSystemLogWorkCorrectly()
    {
        var store = new TerminalStore();
        var selectionEvents = 0;
        var logEvents = 0;
        var resetEvents = 0;

        using var s1 = store.Bus.Subscribe<ActiveRobotChangedEvent>(_ => selectionEvents++);
        using var s2 = store.Bus.Subscribe<SystemEventAppendedEvent>(_ => logEvents++);
        using var s3 = store.Bus.Subscribe<DemoStateResetEvent>(_ => resetEvents++);

        Assert.True(store.SelectRobot(7, TeamSide.Blue));
        Assert.Equal(TeamSide.Blue, store.State.ActiveTeam);
        Assert.Equal(7, store.State.GetActiveRobot()!.Id);
        Assert.Equal(1, selectionEvents);

        // Invalid selections are inert.
        Assert.False(store.SelectRobot(5, TeamSide.Blue));
        Assert.False(store.SelectRobot(3, "invalid"));
        Assert.Equal(7, store.State.ActiveRobotId);
        Assert.Equal(1, selectionEvents);

        // Same selection does not re-emit.
        Assert.True(store.SelectRobot(7, TeamSide.Blue));
        Assert.Equal(1, selectionEvents);

        // Default team is red.
        Assert.True(store.SelectRobot(4));
        Assert.Equal(TeamSide.Red, store.State.ActiveTeam);
        Assert.Equal(4, store.State.ActiveRobotId);
        Assert.Equal(2, selectionEvents);

        // Whitespace log entries are ignored.
        store.AppendSystemEvent("   ");
        Assert.Equal(0, logEvents);

        for (var i = 0; i < TerminalStore.MaxEvents + 10; i++)
        {
            store.AppendSystemEvent($"event-{i}");
        }
        Assert.Equal(TerminalStore.MaxEvents, store.State.Events.Count);
        Assert.Equal("event-10", store.State.Events[0]);
        Assert.Equal("event-59", store.State.Events[^1]);

        var previousState = store.State;
        store.UpdateCredits(1);
        store.ResetDemo();
        Assert.NotSame(previousState, store.State);
        Assert.Equal(650, store.State.Credits);
        Assert.Equal(3, store.State.ActiveRobotId);
        Assert.Equal(TeamSide.Red, store.State.ActiveTeam);
        Assert.Equal(116, store.State.RemainingSeconds);
        Assert.Equal(4, store.State.Events.Count);
        Assert.Equal(1, resetEvents);
    }

    [Fact]
    public void VideoStatusPublishesVideoAndModuleEventsIndependently()
    {
        var store = new TerminalStore();
        VideoStatusUpdatedEvent? lastVideo = null;
        ModuleStatusUpdatedEvent? lastModule = null;

        using var s1 = store.Bus.Subscribe<VideoStatusUpdatedEvent>(e => lastVideo = e);
        using var s2 = store.Bus.Subscribe<ModuleStatusUpdatedEvent>(e => lastModule = e);

        store.SetVideoStatus("图传 · 1920×1080 · 60 fps", true);

        Assert.NotNull(lastVideo);
        Assert.True(lastVideo!.Live);
        Assert.NotNull(lastModule);
        Assert.Equal("video", lastModule!.ModuleKey);
        Assert.True(lastModule.Online);
    }

    [Fact]
    public void SetConnectionStatusDeduplicatesUnchangedValues()
    {
        var store = new TerminalStore();
        var count = 0;
        using var sub = store.Bus.Subscribe<ConnectionStatusChangedEvent>(_ => count++);

        // Same as initial defaults ("演示数据 · 未连接", false) -> no event.
        store.SetConnectionStatus("演示数据 · 未连接", false);
        Assert.Equal(0, count);

        store.SetConnectionStatus("MQTT 已连接", true);
        store.SetConnectionStatus("MQTT 已连接", true);
        Assert.Equal(1, count);
        Assert.True(store.State.IsConnected);

        store.SetConnectionStatus("MQTT 重连中", false);
        Assert.Equal(2, count);
        Assert.False(store.State.IsConnected);
    }

    [Fact]
    public void UpdateRobotHeatAndPowerDeduplicatesWithinTolerance()
    {
        var store = new TerminalStore();
        var count = 0;
        using var sub = store.Bus.Subscribe<RobotHeatPowerUpdatedEvent>(_ => count++);

        store.UpdateRobotHeatAndPower(TeamSide.Red, 3, 50.0f, 60.0f);
        store.UpdateRobotHeatAndPower(TeamSide.Red, 3, 50.00005f, 60.00005f);
        Assert.Equal(1, count);

        store.UpdateRobotHeatAndPower(TeamSide.Red, 3, 50.5f, 60.0f);
        Assert.Equal(2, count);
    }

    [Fact]
    public void CustomInitialStateFactoryDecouplesStoreFromDemoScenario()
    {
        var store = new TerminalStore(initialStateFactory: () => new MatchState
        {
            RemainingSeconds = 300,
            Credits = 0,
            ConnectionLabel = "等待连接",
        });

        Assert.Equal(300, store.State.RemainingSeconds);
        Assert.Equal(0, store.State.Credits);
        Assert.Equal("等待连接", store.State.ConnectionLabel);
        Assert.Empty(store.State.Red.Robots);
    }
}
