using System;
using RmClient.Core.Events;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Events.Tactical;

namespace RmClient.Core.State;

/// <summary>
/// Owns domain state slices and publishes single-purpose events on every state mutation.
/// Completely decoupled from Godot scene nodes, network transports, and demo fixtures.
/// </summary>
public sealed class TerminalStore
{
    public const int MaxEvents = 50;
    private const float FloatTolerance = 0.0001f;

    private readonly Func<MatchState> _initialStateFactory;

    public IEventBus Bus { get; }
    public MatchState State { get; private set; }

    public TerminalStore(IEventBus? eventBus = null, Func<MatchState>? initialStateFactory = null)
    {
        Bus = eventBus ?? new EventBus();
        _initialStateFactory = initialStateFactory ?? DemoScenario.CreateInitialState;
        State = _initialStateFactory();
    }

    /// <summary>
    /// Selects an existing robot. Invalid selections leave the current selection intact.
    /// </summary>
    public bool SelectRobot(int robotId, TeamSide side = TeamSide.Red)
    {
        var team = State.GetTeam(side);
        if (team.FindRobot(robotId) is null)
        {
            return false;
        }
        if (State.ActiveRobotId == robotId && State.ActiveTeam == side)
        {
            return true;
        }

        State.ActiveRobotId = robotId;
        State.ActiveTeam = side;
        Bus.Publish(new ActiveRobotChangedEvent(side, robotId));
        return true;
    }

    public bool SelectRobot(int robotId, string? sideCode)
    {
        return DomainLabels.TryParseTeamSide(sideCode, out var side) && SelectRobot(robotId, side);
    }

    public void UpdateClock(int remainingSeconds, int? roundIndex = null)
    {
        var clampedSeconds = Math.Max(remainingSeconds, 0);
        var nextRound = roundIndex ?? State.RoundIndex;
        if (State.RemainingSeconds == clampedSeconds && State.RoundIndex == nextRound)
        {
            return;
        }
        State.RemainingSeconds = clampedSeconds;
        State.RoundIndex = nextRound;
        Bus.Publish(new MatchClockUpdatedEvent(State.RoundIndex, State.RemainingSeconds));
    }

    public void UpdateCredits(int credits)
    {
        if (State.Credits == credits)
        {
            return;
        }
        State.Credits = credits;
        Bus.Publish(new CreditsUpdatedEvent(credits));
    }

    public void UpdateRobotHealth(TeamSide side, int robotId, int health, int? maxHealth = null, bool? online = null)
    {
        var robot = State.GetTeam(side).FindRobot(robotId);
        if (robot is null)
        {
            return;
        }
        var nextMax = maxHealth ?? robot.MaxHealth;
        var nextOnline = online ?? robot.Online;
        if (robot.Health == health && robot.MaxHealth == nextMax && robot.Online == nextOnline)
        {
            return;
        }
        robot.Health = health;
        robot.MaxHealth = nextMax;
        robot.Online = nextOnline;
        Bus.Publish(new RobotHealthUpdatedEvent(side, robotId, robot.Health, robot.MaxHealth, robot.Online));
    }

    public void UpdateRobotAmmo(TeamSide side, int robotId, int ammo, float? muzzleSpeed = null)
    {
        var robot = State.GetTeam(side).FindRobot(robotId);
        if (robot is null)
        {
            return;
        }
        var nextSpeed = muzzleSpeed ?? robot.MuzzleSpeed;
        if (robot.Ammo == ammo && Math.Abs(robot.MuzzleSpeed - nextSpeed) < FloatTolerance)
        {
            return;
        }
        robot.Ammo = ammo;
        robot.MuzzleSpeed = nextSpeed;
        Bus.Publish(new RobotAmmoUpdatedEvent(side, robotId, robot.Ammo, robot.MuzzleSpeed));
    }

    public void UpdateRobotHeatAndPower(TeamSide side, int robotId, float heat, float power)
    {
        var robot = State.GetTeam(side).FindRobot(robotId);
        if (robot is null)
        {
            return;
        }
        var clampedHeat = Math.Clamp(heat, 0f, robot.HeatLimit);
        var clampedPower = Math.Min(robot.PowerLimit, Math.Max(0f, power));
        if (Math.Abs(robot.Heat - clampedHeat) < FloatTolerance
            && Math.Abs(robot.Power - clampedPower) < FloatTolerance)
        {
            return;
        }
        robot.Heat = clampedHeat;
        robot.Power = clampedPower;
        Bus.Publish(new RobotHeatPowerUpdatedEvent(
            side,
            robotId,
            robot.Heat,
            robot.HeatLimit,
            robot.Power,
            robot.PowerLimit));
    }

    public void UpdateRobotEnergy(TeamSide side, int robotId, float chassisEnergy, float? bufferEnergy = null)
    {
        var robot = State.GetTeam(side).FindRobot(robotId);
        if (robot is null)
        {
            return;
        }
        var nextBuffer = bufferEnergy ?? robot.BufferEnergy;
        if (Math.Abs(robot.ChassisEnergy - chassisEnergy) < FloatTolerance
            && Math.Abs(robot.BufferEnergy - nextBuffer) < FloatTolerance)
        {
            return;
        }
        robot.ChassisEnergy = chassisEnergy;
        robot.BufferEnergy = nextBuffer;
        Bus.Publish(new RobotEnergyUpdatedEvent(side, robotId, robot.ChassisEnergy, robot.BufferEnergy));
    }

    public void UpdateRobotPosition(TeamSide side, int robotId, NormalizedPoint position)
    {
        var robot = State.GetTeam(side).FindRobot(robotId);
        if (robot is null || !position.IsInBounds)
        {
            return;
        }
        if (robot.MapPosition == position)
        {
            return;
        }
        robot.MapPosition = position;
        Bus.Publish(new RobotPositionUpdatedEvent(side, robotId, position));
    }

    public void UpdateBaseHealth(TeamSide side, int baseHealth, int baseMaxHealth, int outpostHealth)
    {
        var team = State.GetTeam(side);
        if (team.BaseHealth == baseHealth
            && team.BaseMaxHealth == baseMaxHealth
            && team.OutpostHealth == outpostHealth)
        {
            return;
        }
        team.BaseHealth = baseHealth;
        team.BaseMaxHealth = baseMaxHealth;
        team.OutpostHealth = outpostHealth;
        Bus.Publish(new BaseHealthUpdatedEvent(side, baseHealth, baseMaxHealth, outpostHealth));
    }

    public void UpdateTeamScore(TeamSide side, int score)
    {
        var team = State.GetTeam(side);
        if (team.Score == score)
        {
            return;
        }
        team.Score = score;
        Bus.Publish(new TeamScoreUpdatedEvent(side, score));
    }

    public void SetModuleStatus(string moduleKey, bool online)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return;
        }
        if (State.Modules.TryGetValue(moduleKey, out var current) && current == online)
        {
            return;
        }
        State.Modules[moduleKey] = online;
        Bus.Publish(new ModuleStatusUpdatedEvent(moduleKey, online));
    }

    public void SetVideoStatus(string label, bool live, string error = "")
    {
        var currentLive = State.Modules.TryGetValue("video", out var existing) && existing;
        if (State.VideoLabel == label && currentLive == live && string.IsNullOrEmpty(error))
        {
            return;
        }
        State.VideoLabel = label;
        State.Modules["video"] = live;
        Bus.Publish(new VideoStatusUpdatedEvent(label, live, error));
        if (currentLive != live)
        {
            Bus.Publish(new ModuleStatusUpdatedEvent("video", live));
        }
    }

    public void SetConnectionStatus(string label, bool connected)
    {
        if (State.ConnectionLabel == label && State.IsConnected == connected)
        {
            return;
        }
        State.ConnectionLabel = label;
        State.IsConnected = connected;
        Bus.Publish(new ConnectionStatusChangedEvent(label, connected));
    }

    public void SetRobotMessage(string message)
    {
        if (State.RobotMessage == message)
        {
            return;
        }
        State.RobotMessage = message;
        Bus.Publish(new RobotMessageUpdatedEvent(message));
    }

    public void AppendSystemEvent(string message)
    {
        var clean = message?.Trim() ?? string.Empty;
        if (clean.Length == 0)
        {
            return;
        }
        State.Events.Add(clean);
        while (State.Events.Count > MaxEvents)
        {
            State.Events.RemoveAt(0);
        }
        Bus.Publish(new SystemEventAppendedEvent(clean));
    }

    /// <summary>
    /// Replaces the state with a fresh baseline from the configured factory and notifies subscribers.
    /// </summary>
    public void ResetDemo()
    {
        State = _initialStateFactory();
        Bus.Publish(new DemoStateResetEvent());
    }
}
