using System;
using System.Collections.Generic;

namespace RmClient.Core.Events;

/// <summary>
/// Marker interface for all single-purpose domain events.
/// </summary>
public interface IDomainEvent
{
}

/// <summary>
/// Thread-safe, strongly-typed publish/subscribe bus for independent domain events.
/// </summary>
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;

    void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
}

/// <summary>
/// Default synchronous in-process event bus.
/// Each event type maintains an isolated subscriber list so handlers remain decoupled.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var eventType = typeof(TEvent);
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(eventType, out var list))
            {
                list = new List<Delegate>();
                _subscribers[eventType] = list;
            }
            list.Add(handler);
        }

        return new Subscription(() => Unsubscribe(eventType, handler));
    }

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        Delegate[] snapshot;
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return;
            }
            snapshot = list.ToArray();
        }

        foreach (var entry in snapshot)
        {
            ((Action<TEvent>)entry).Invoke(domainEvent);
        }
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(eventType, out var list))
            {
                return;
            }
            list.Remove(handler);
            if (list.Count == 0)
            {
                _subscribers.Remove(eventType);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _onDispose;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            var action = System.Threading.Interlocked.Exchange(ref _onDispose, null);
            action?.Invoke();
        }
    }
}
