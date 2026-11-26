using System;
using RmClient.Core.Events;
using RmClient.Core.Events.Match;
using RmClient.Core.Events.Robot;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Events.Tactical;

namespace RmClient.Core.State;

/// <summary>
/// Owns domain state slices and publishes single-purpose events on every state mutation.
/// Completely decoupled from Godot scene nodes and network transports.
/// </summary>
public sealed class TerminalStore
{
    public const int MaxEvents = 50;
    public const int DemoDurationSeconds = 116;

    public static readonly int[] DemoRobotIds = { 1, 2, 3, 4, 6, 7 };

    public static readonly NormalizedPoint[] DemoRedPositions =
    {
        new(0.18f, 0.76f),
        new(0.23f, 0.63f),
        new(0.37f, 0.65f),
        new(0.47f, 0.78f),
        new(0.30f, 0.50f),
        new(0.14f, 0.55f),
    };

    public IEventBus Bus { get; }
    public MatchState State { get; private set; }

    public TerminalStore(IEventBus? eventBus = null)
    {
        Bus = eventBus ?? new EventBus();
        State = CreateDemoState();
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
        if (robot.Ammo == ammo && Math.Abs(robot.MuzzleSpeed - nextSpeed) < 0.0001f)
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
        robot.ChassisEnergy = chassisEnergy;
        if (bufferEnergy.HasValue)
        {
            robot.BufferEnergy = bufferEnergy.Value;
        }
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
        State.ConnectionLabel = label;
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
    /// Replaces the state with a fresh demo baseline and notifies subscribers.
    /// </summary>
    public void ResetDemo()
    {
        State = CreateDemoState();
        Bus.Publish(new DemoStateResetEvent());
    }

    public static MatchState CreateDemoState()
    {
        var state = new MatchState
        {
            RemainingSeconds = DemoDurationSeconds,
            Red = CreateDemoTeam(TeamSide.Red),
            Blue = CreateDemoTeam(TeamSide.Blue),
        };

        // Simulated HUD module states, not real hardware/driver detection.
        state.Modules["chassis"] = true;
        state.Modules["gimbal"] = true;
        state.Modules["shooter"] = true;
        state.Modules["referee"] = true;
        state.Modules["video"] = false;
        state.Modules["custom_terminal"] = false;

        state.Events.Add("演示：快照已加载，未连接赛事引擎或通信后端。");
        state.Events.Add("演示：红方 3 号步兵等级 1，升级进度 65%。");
        state.Events.Add("演示：当前金币 650，非真实比赛结算。");
        state.Events.Add("演示：购买、兑换和战术指令只修改本地数据。");
        return state;
    }

    private static TeamState CreateDemoTeam(TeamSide side)
    {
        var team = new TeamState
        {
            Side = side,
            Title = side.ToDisplayTitle(),
        };

        for (var i = 0; i < DemoRobotIds.Length; i++)
        {
            var id = DemoRobotIds[i];
            var (title, maxHealth) = id switch
            {
                1 => ("英雄", 350),
                2 => ("重装", 300),
                3 or 4 => ("步兵", 225),
                6 => ("空中", 200),
                7 => ("哨兵", 400),
                _ => ("机器人", 225),
            };

            var position = side == TeamSide.Blue
                ? DemoRedPositions[i].Mirrored()
                : DemoRedPositions[i];

            team.Robots.Add(new RobotState
            {
                Id = id,
                Title = title,
                MaxHealth = maxHealth,
                Health = maxHealth,
                MapPosition = position,
            });
        }

        return team;
    }
}
