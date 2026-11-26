using System;
using RmClient.Core.Events;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.State;
using Xunit;

namespace RmClient.Core.Tests;

public sealed class EventBusTests
{
    [Fact]
    public void EventsAreIsolatedByType()
    {
        var bus = new EventBus();
        var clockCount = 0;
        var creditCount = 0;

        using var s1 = bus.Subscribe<MatchClockUpdatedEvent>(_ => clockCount++);
        using var s2 = bus.Subscribe<CreditsUpdatedEvent>(_ => creditCount++);

        bus.Publish(new MatchClockUpdatedEvent(1, 100));

        Assert.Equal(1, clockCount);
        Assert.Equal(0, creditCount);
    }

    [Fact]
    public void UnsubscribeStopsDeliveryAndIsIdempotent()
    {
        var bus = new EventBus();
        var count = 0;

        var sub = bus.Subscribe<CreditsUpdatedEvent>(_ => count++);
        bus.Publish(new CreditsUpdatedEvent(500));
        sub.Dispose();
        sub.Dispose();
        bus.Publish(new CreditsUpdatedEvent(400));

        Assert.Equal(1, count);
    }

    [Fact]
    public void UnsubscribingDuringPublishDoesNotThrow()
    {
        var bus = new EventBus();
        var hits = 0;
        IDisposable? sub = null;
        sub = bus.Subscribe<ActiveRobotChangedEvent>(_ =>
        {
            hits++;
            sub?.Dispose();
        });
        using var sub2 = bus.Subscribe<ActiveRobotChangedEvent>(_ => hits++);

        bus.Publish(new ActiveRobotChangedEvent(TeamSide.Blue, 7));
        bus.Publish(new ActiveRobotChangedEvent(TeamSide.Red, 3));

        Assert.Equal(3, hits);
    }
}
