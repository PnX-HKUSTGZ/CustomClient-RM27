namespace RmClient.Core.State;

/// <summary>
/// Deterministic local demo baseline constants and initial MatchState factory.
/// Kept separate from TerminalStore so live transports can supply their own initial state.
/// </summary>
public static class DemoScenario
{
    public const int DurationSeconds = 116;

    public static readonly int[] RobotIds = { 1, 2, 3, 4, 6, 7 };

    public static readonly NormalizedPoint[] RedPositions =
    {
        new(0.18f, 0.76f),
        new(0.23f, 0.63f),
        new(0.37f, 0.65f),
        new(0.47f, 0.78f),
        new(0.30f, 0.50f),
        new(0.14f, 0.55f),
    };

    public static MatchState CreateInitialState()
    {
        var state = new MatchState
        {
            RemainingSeconds = DurationSeconds,
            Red = CreateTeam(TeamSide.Red),
            Blue = CreateTeam(TeamSide.Blue),
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

    private static TeamState CreateTeam(TeamSide side)
    {
        var team = new TeamState
        {
            Side = side,
            Title = side.ToDisplayTitle(),
        };

        for (var i = 0; i < RobotIds.Length; i++)
        {
            var id = RobotIds[i];
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
                ? RedPositions[i].Mirrored()
                : RedPositions[i];

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
