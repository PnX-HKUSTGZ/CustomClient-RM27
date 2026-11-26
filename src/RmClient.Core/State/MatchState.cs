using System;
using System.Collections.Generic;

namespace RmClient.Core.State;

/// <summary>
/// UI-facing robot state slice. Positions use normalized arena coordinates, not protocol units.
/// </summary>
public sealed class RobotState
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Health { get; set; } = 225;
    public int MaxHealth { get; set; } = 225;
    public int Ammo { get; set; } = 150;
    public int Level { get; set; } = 1;
    public float Heat { get; set; } = 24f;
    public float HeatLimit { get; set; } = 240f;
    public float MuzzleSpeed { get; set; } = 29.5f;
    public float Power { get; set; } = 52f;
    public float PowerLimit { get; set; } = 80f;
    public float BufferEnergy { get; set; } = 40f;
    public float ChassisEnergy { get; set; } = 75f;
    public float ExpRatio { get; set; } = 0.65f;
    public NormalizedPoint MapPosition { get; set; } = NormalizedPoint.Zero;
    public bool Online { get; set; } = true;
}

public sealed class TeamState
{
    public TeamSide Side { get; set; } = TeamSide.Red;
    public string Title { get; set; } = "红方";
    public int BaseHealth { get; set; } = 5000;
    public int BaseMaxHealth { get; set; } = 5000;
    public int OutpostHealth { get; set; } = 1500;
    public int Score { get; set; }
    public List<RobotState> Robots { get; } = new();

    public RobotState? FindRobot(int robotId)
    {
        foreach (var robot in Robots)
        {
            if (robot.Id == robotId)
            {
                return robot;
            }
        }
        return null;
    }

    public int AliveCount()
    {
        var count = 0;
        foreach (var robot in Robots)
        {
            if (robot.Online && robot.Health > 0)
            {
                count++;
            }
        }
        return count;
    }

    public int TotalHealth()
    {
        var total = 0;
        foreach (var robot in Robots)
        {
            total += Math.Max(robot.Health, 0);
        }
        return total;
    }
}

public sealed class MatchState
{
    public int RemainingSeconds { get; set; } = TerminalStore.DemoDurationSeconds;
    public int RoundIndex { get; set; } = 1;
    public int Credits { get; set; } = 650;
    public TeamState Red { get; set; } = new() { Side = TeamSide.Red, Title = "红方" };
    public TeamState Blue { get; set; } = new() { Side = TeamSide.Blue, Title = "蓝方" };
    public TeamSide ActiveTeam { get; set; } = TeamSide.Red;
    public int ActiveRobotId { get; set; } = 3;
    public Dictionary<string, bool> Modules { get; } = new(StringComparer.Ordinal);
    public List<string> Events { get; } = new();
    public string RobotMessage { get; set; } = "等待本地演示指令";
    public string ConnectionLabel { get; set; } = "演示数据 · 未连接";
    public string VideoLabel { get; set; } = "图传 · 未启用";

    public TeamState GetTeam(TeamSide side) => side == TeamSide.Red ? Red : Blue;

    public TeamState? GetTeam(string? sideCode)
    {
        return DomainLabels.TryParseTeamSide(sideCode, out var side) ? GetTeam(side) : null;
    }

    public RobotState? GetActiveRobot()
    {
        return GetTeam(ActiveTeam).FindRobot(ActiveRobotId);
    }
}
