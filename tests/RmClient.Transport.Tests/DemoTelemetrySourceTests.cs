using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;
using RmClient.Transport.Demo;
using Xunit;

namespace RmClient.Transport.Tests;

public sealed class DemoTelemetrySourceTests
{
    [Fact]
    public void StartAndTickAdvanceDeterministicallyViaDomainEvents()
    {
        var store = new TerminalStore();
        var demo = new DemoTelemetrySource(store);
        var clockTicks = 0;
        var heatTicks = 0;

        using var s1 = store.Bus.Subscribe<MatchClockUpdatedEvent>(_ => clockTicks++);
        using var s2 = store.Bus.Subscribe<RobotHeatPowerUpdatedEvent>(_ => heatTicks++);

        demo.Start();
        Assert.True(demo.IsRunning);
        Assert.Equal(1, clockTicks);
        Assert.Equal(116, store.State.RemainingSeconds);

        // Sub-second ticks do not advance countdown.
        demo.Tick(0.4);
        demo.Tick(0.4);
        Assert.Equal(116, store.State.RemainingSeconds);

        // Crossing 1.0 advances exactly one second and updates 12 robots.
        demo.Tick(0.25);
        Assert.Equal(115, store.State.RemainingSeconds);
        Assert.Equal(2, clockTicks);
        Assert.Equal(12, heatTicks);

        // Paused does not advance.
        demo.SetPaused(true);
        demo.Tick(5.0);
        Assert.Equal(115, store.State.RemainingSeconds);

        // Resuming and exhausting countdown stops cleanly and appends log.
        demo.SetPaused(false);
        demo.Tick(120.0);
        Assert.Equal(0, store.State.RemainingSeconds);
        Assert.False(demo.IsRunning);
        Assert.Contains("倒计时结束", store.State.Events[^1]);
        Assert.Equal(650, store.State.Credits);
    }
}
