using System.Collections.Generic;
using RmClient.Core.Commands;
using RmClient.Core.Events.Commands;
using RmClient.Core.Events.Tactical;
using RmClient.Core.State;
using Xunit;

namespace RmClient.Core.Tests;

public sealed class TerminalCommandsTests
{
    [Fact]
    public void PurchaseAmmoEnforcesQuotesLimitsAndAtomicity()
    {
        var store = new TerminalStore();
        var commands = new TerminalCommands(store);
        var feedbacks = new List<CommandFeedbackEvent>();
        using var sub = store.Bus.Subscribe<CommandFeedbackEvent>(feedbacks.Add);

        Assert.Equal(10, TerminalCommands.GetPurchaseCost(10, 17));
        Assert.Equal(50, TerminalCommands.GetPurchaseCost(5, 42));
        Assert.Equal(-1, TerminalCommands.GetPurchaseCost(0, 17));
        Assert.Equal(-1, TerminalCommands.GetPurchaseCost(501, 17));
        Assert.Equal(-1, TerminalCommands.GetPurchaseCost(1, 18));

        Assert.True(commands.PurchaseAmmo(10, 17));
        Assert.Equal(640, store.State.Credits);
        Assert.Equal(160, store.State.GetActiveRobot()!.Ammo);
        Assert.True(feedbacks[^1].Accepted);

        Assert.True(commands.PurchaseAmmo(5, 42));
        Assert.Equal(590, store.State.Credits);
        Assert.Equal(165, store.State.GetActiveRobot()!.Ammo);

        // Insufficient balance must not partially mutate state.
        store.UpdateCredits(49);
        Assert.False(commands.PurchaseAmmo(50, 17));
        Assert.Equal(49, store.State.Credits);
        Assert.Equal(165, store.State.GetActiveRobot()!.Ammo);
        Assert.False(feedbacks[^1].Accepted);

        // Exact capacity allowed; exceeding rejected.
        store.UpdateCredits(10000);
        store.UpdateRobotAmmo(TeamSide.Red, 3, TerminalCommands.MaxAmmo - 1);
        Assert.True(commands.PurchaseAmmo(1, 17));
        Assert.False(commands.PurchaseAmmo(1, 17));
        Assert.Equal(TerminalCommands.MaxAmmo, store.State.GetActiveRobot()!.Ammo);
    }

    [Fact]
    public void RemoteExchangeValidatesAllFourKinds()
    {
        var store = new TerminalStore();
        var commands = new TerminalCommands(store);
        store.UpdateCredits(2000);

        // Health full -> rejected.
        Assert.False(commands.RemoteExchange(RemoteExchangeKind.Health));
        Assert.Equal(2000, store.State.Credits);

        // Injured -> healed up to max.
        store.UpdateRobotHealth(TeamSide.Red, 3, 180);
        Assert.True(commands.RemoteExchange(RemoteExchangeKind.Health));
        Assert.Equal(225, store.State.GetActiveRobot()!.Health);
        Assert.Equal(1800, store.State.Credits);

        // Energy +25 clamped to 100, then rejected when full.
        Assert.True(commands.RemoteExchange(RemoteExchangeKind.Energy));
        Assert.Equal(100f, store.State.GetActiveRobot()!.ChassisEnergy);
        Assert.False(commands.RemoteExchange(RemoteExchangeKind.Energy));

        // Air support allowed once.
        Assert.True(commands.RemoteExchange(RemoteExchangeKind.AirSupport));
        Assert.True(store.State.Modules["air_support"]);
        Assert.False(commands.RemoteExchange(RemoteExchangeKind.AirSupport));

        // Ammo +50.
        var ammoBefore = store.State.GetActiveRobot()!.Ammo;
        Assert.True(commands.RemoteExchange("ammo"));
        Assert.Equal(ammoBefore + 50, store.State.GetActiveRobot()!.Ammo);

        // Invalid kind rejected.
        Assert.False(commands.RemoteExchange("unknown"));
    }

    [Fact]
    public void MapMarkerValidatesCoordinatesKindsReceiversAndPublishesEvent()
    {
        var store = new TerminalStore();
        var commands = new TerminalCommands(store);
        MapMarkerSubmittedEvent? received = null;
        using var sub = store.Bus.Subscribe<MapMarkerSubmittedEvent>(e => received = e);

        Assert.False(commands.SubmitMapMarker(new NormalizedPoint(float.NaN, 0.5f), "attack", 3));
        Assert.False(commands.SubmitMapMarker(new NormalizedPoint(1.1f, 0.5f), "attack", 3));
        Assert.False(commands.SubmitMapMarker(new NormalizedPoint(0.5f, 0.5f), "bogus", 3));
        Assert.False(commands.SubmitMapMarker(new NormalizedPoint(0.5f, 0.5f), "attack", 99));
        Assert.Null(received);

        Assert.True(commands.SubmitMapMarker(new NormalizedPoint(0.25f, 0.75f), "defend", 3));
        Assert.NotNull(received);
        Assert.Equal(MapMarkerKind.Defend, received!.Kind);
        Assert.Equal(3, received.ReceiverId);

        Assert.True(commands.SubmitMapMarker(new NormalizedPoint(0.5f, 0.5f), "alert", TerminalCommands.TeamReceiver));
        Assert.Equal(TerminalCommands.TeamReceiver, received.ReceiverId);
    }

    [Fact]
    public void LoginDeploymentRuneAndResetCommandsWork()
    {
        var store = new TerminalStore();
        var commands = new TerminalCommands(store);

        Assert.False(commands.SelectLogin(99, "red"));
        Assert.False(commands.SelectLogin(3, "green"));
        Assert.True(commands.SelectLogin(6, "blue"));
        Assert.Equal(TeamSide.Blue, store.State.ActiveTeam);
        Assert.Equal(6, store.State.ActiveRobotId);

        Assert.True(commands.TriggerDeployment());
        Assert.True(store.State.Modules["deployment"]);

        Assert.True(commands.TriggerRune());
        Assert.True(store.State.Modules["rune"]);

        Assert.True(commands.ResetDemo());
        Assert.Equal(TeamSide.Red, store.State.ActiveTeam);
        Assert.Equal(3, store.State.ActiveRobotId);
        Assert.Equal(650, store.State.Credits);
    }
}
