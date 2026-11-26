using System;
using System.Collections.Generic;
using RmClient.Core.Events;
using RmClient.Core.Events.Runtime;
using RmClient.Transport.Abstractions;

namespace RmClient.Transport.Peripherals;

public sealed record PeripheralDescriptor(string Title, string Kind, IReadOnlyList<string> Channels);

/// <summary>
/// Peripheral adaptation boundary for custom controllers (e.g. robotic arms),
/// extra buttons, and vehicle-specific inputs. Reserved interface; concrete drivers
/// keep serial/HID/UDP framing inside their implementations.
/// </summary>
public interface IPeripheralSource
{
    string PeripheralId { get; }
    bool IsConnected { get; }

    event Action<bool>? ConnectionChanged;
    event Action<string, object?>? InputReceived;
    event Action<string>? FaultOccurred;

    PeripheralDescriptor Describe();
    bool Start();
    void Stop();
    bool Send(string channel, object? value);
}

/// <summary>
/// Registry and lifecycle manager for IPeripheralSource adapters.
/// Marshals peripheral callbacks onto the main thread and publishes domain events on IEventBus.
/// </summary>
public sealed class PeripheralHub : IDisposable
{
    private readonly IEventBus _bus;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public PeripheralHub(IEventBus bus, IMainThreadDispatcher? dispatcher = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _dispatcher = dispatcher ?? new ImmediateDispatcher();
    }

    public bool Register(IPeripheralSource source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.PeripheralId))
        {
            return false;
        }
        var id = source.PeripheralId;
        if (_entries.ContainsKey(id))
        {
            return false;
        }

        Action<bool> onConnection = connected =>
            _dispatcher.Post(() => _bus.Publish(new PeripheralConnectionChangedEvent(id, connected)));
        Action<string, object?> onInput = (channel, value) =>
            _dispatcher.Post(() => _bus.Publish(new PeripheralInputReceivedEvent(id, channel, value)));
        Action<string> onFault = message =>
            _dispatcher.Post(() => _bus.Publish(new PeripheralFaultEvent(id, message)));

        source.ConnectionChanged += onConnection;
        source.InputReceived += onInput;
        source.FaultOccurred += onFault;

        _entries[id] = new Entry(source, onConnection, onInput, onFault);
        return true;
    }

    public void Unregister(string peripheralId)
    {
        if (!_entries.Remove(peripheralId, out var entry))
        {
            return;
        }
        DetachAndStop(entry);
    }

    public IPeripheralSource? GetSource(string peripheralId)
    {
        return _entries.TryGetValue(peripheralId, out var entry) ? entry.Source : null;
    }

    public IReadOnlyCollection<string> GetIds() => _entries.Keys;

    public void StartAll()
    {
        foreach (var entry in _entries.Values)
        {
            if (!entry.Source.Start())
            {
                _dispatcher.Post(() => _bus.Publish(new PeripheralFaultEvent(entry.Source.PeripheralId, "外设启动失败")));
            }
        }
    }

    public void StopAll()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Source.Stop();
        }
    }

    public bool Send(string peripheralId, string channel, object? value)
    {
        return _entries.TryGetValue(peripheralId, out var entry) && entry.Source.Send(channel, value);
    }

    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            DetachAndStop(entry);
        }
        _entries.Clear();
    }

    private static void DetachAndStop(Entry entry)
    {
        entry.Source.Stop();
        entry.Source.ConnectionChanged -= entry.OnConnection;
        entry.Source.InputReceived -= entry.OnInput;
        entry.Source.FaultOccurred -= entry.OnFault;
    }

    private sealed record Entry(
        IPeripheralSource Source,
        Action<bool> OnConnection,
        Action<string, object?> OnInput,
        Action<string> OnFault);
}
