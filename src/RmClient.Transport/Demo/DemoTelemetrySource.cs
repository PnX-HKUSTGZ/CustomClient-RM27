using System;
using RmClient.Core.Events.Match;
using RmClient.Core.State;
using RmClient.Transport.Abstractions;

namespace RmClient.Transport.Demo;

/// <summary>
/// Deterministic local clock and robot telemetry source without UI or network dependencies.
/// Publishes single-purpose domain events through TerminalStore.
/// </summary>
public sealed class DemoTelemetrySource : ITelemetrySource
{
    private readonly TerminalStore _store;
    private double _accumulatedSeconds;

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    public DemoTelemetrySource(TerminalStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Publishes the initial clock state immediately; countdown ticks on each elapsed whole second.
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }
        _accumulatedSeconds = 0.0;
        IsRunning = _store.State.RemainingSeconds > 0;
        _store.Bus.Publish(new MatchClockUpdatedEvent(_store.State.RoundIndex, _store.State.RemainingSeconds));
    }

    public void Stop()
    {
        IsRunning = false;
        _accumulatedSeconds = 0.0;
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    public void Tick(double deltaSeconds)
    {
        if (!IsRunning || IsPaused || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0.0)
        {
            return;
        }

        _accumulatedSeconds += deltaSeconds;
        // Catch up whole seconds deterministically without frame-rate dependence.
        while (IsRunning && _accumulatedSeconds >= 1.0)
        {
            _accumulatedSeconds -= 1.0;
            AdvanceOneSecond();
        }
    }

    private void AdvanceOneSecond()
    {
        var state = _store.State;
        if (state.RemainingSeconds <= 0)
        {
            Stop();
            return;
        }

        var nextSeconds = state.RemainingSeconds - 1;
        _store.UpdateClock(nextSeconds);

        var elapsed = DemoScenario.DurationSeconds - nextSeconds;
        foreach (var team in new[] { state.Red, state.Blue })
        {
            foreach (var robot in team.Robots)
            {
                if (!robot.Online)
                {
                    continue;
                }
                var heatPulse = elapsed % 8 == 0 ? 6.0f : 0.0f;
                var nextHeat = Math.Clamp(robot.Heat - 2.5f + heatPulse, 0.0f, robot.HeatLimit);
                var nextPower = Math.Min(robot.PowerLimit, 48.0f + ((elapsed + robot.Id * 3) % 12));
                _store.UpdateRobotHeatAndPower(team.Side, robot.Id, nextHeat, nextPower);
            }
        }

        // Never move map markers, spend credits, consume purchased ammo, or fake video.
        if (nextSeconds == 0)
        {
            _store.AppendSystemEvent("演示：本地比赛倒计时结束，仍未连接通信后端。");
            Stop();
        }
    }
}
