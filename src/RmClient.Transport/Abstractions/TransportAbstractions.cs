using System;

namespace RmClient.Transport.Abstractions;

/// <summary>
/// Transport boundary for match telemetry sources (local demo, MQTT/RM2027, replay).
/// </summary>
public interface ITelemetrySource
{
    bool IsRunning { get; }
    bool IsPaused { get; }

    void Start();
    void Stop();
    void SetPaused(bool paused);
    void Tick(double deltaSeconds);
}

/// <summary>
/// Marshals callbacks from background transport threads onto the UI/main thread.
/// </summary>
public interface IMainThreadDispatcher
{
    void Post(Action action);
}

/// <summary>
/// Inline dispatcher for deterministic unit tests and single-threaded hosts.
/// </summary>
public sealed class ImmediateDispatcher : IMainThreadDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
