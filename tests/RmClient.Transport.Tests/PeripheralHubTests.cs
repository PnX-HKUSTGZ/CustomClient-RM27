using System;
using System.Collections.Generic;
using RmClient.Core.Events;
using RmClient.Core.Events.Runtime;
using RmClient.Transport.Peripherals;
using Xunit;

namespace RmClient.Transport.Tests;

public sealed class PeripheralHubTests
{
    private sealed class FakePeripheral : IPeripheralSource
    {
        public string PeripheralId { get; }
        public bool IsConnected { get; private set; }
        public bool ShouldStart { get; set; } = true;
        public List<(string Channel, object? Value)> Sent { get; } = new();

        public event Action<bool>? ConnectionChanged;
        public event Action<string, object?>? InputReceived;
        public event Action<string>? FaultOccurred;

        public FakePeripheral(string id) => PeripheralId = id;

        public PeripheralDescriptor Describe() => new("Test Arm", "arm", new[] { "arm.joints" });

        public bool Start()
        {
            if (!ShouldStart)
            {
                return false;
            }
            IsConnected = true;
            ConnectionChanged?.Invoke(true);
            return true;
        }

        public void Stop()
        {
            if (IsConnected)
            {
                IsConnected = false;
                ConnectionChanged?.Invoke(false);
            }
        }

        public bool Send(string channel, object? value)
        {
            Sent.Add((channel, value));
            return true;
        }

        public void EmitInput(string channel, object? value) => InputReceived?.Invoke(channel, value);

        public void EmitFault(string message) => FaultOccurred?.Invoke(message);
    }

    [Fact]
    public void HubRoutesLifecycleInputsAndFaultsAsIndependentEvents()
    {
        var bus = new EventBus();
        using var hub = new PeripheralHub(bus);

        var connections = new List<PeripheralConnectionChangedEvent>();
        var inputs = new List<PeripheralInputReceivedEvent>();
        var faults = new List<PeripheralFaultEvent>();

        using var s1 = bus.Subscribe<PeripheralConnectionChangedEvent>(connections.Add);
        using var s2 = bus.Subscribe<PeripheralInputReceivedEvent>(inputs.Add);
        using var s3 = bus.Subscribe<PeripheralFaultEvent>(faults.Add);

        var arm = new FakePeripheral("arm_controller");
        Assert.True(hub.Register(arm));
        Assert.False(hub.Register(new FakePeripheral("arm_controller")));
        Assert.False(hub.Register(new FakePeripheral("")));

        hub.StartAll();
        Assert.Single(connections);
        Assert.True(connections[0].Connected);

        arm.EmitInput("arm.joints", new[] { 0.1f, 0.2f });
        Assert.Single(inputs);
        Assert.Equal("arm.joints", inputs[0].Channel);

        arm.EmitFault("checksum mismatch");
        Assert.Single(faults);
        Assert.Equal("checksum mismatch", faults[0].Message);

        Assert.True(hub.Send("arm_controller", "led.mode", 1));
        Assert.Single(arm.Sent);

        hub.Unregister("arm_controller");
        Assert.Equal(2, connections.Count);
        Assert.False(connections[1].Connected);

        // Events after unregister are no longer forwarded.
        arm.EmitInput("arm.joints", 0);
        Assert.Single(inputs);
    }
}
