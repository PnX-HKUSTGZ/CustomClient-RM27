using System;
using System.Collections.Generic;
using RmClient.Core.Events;
using RmClient.Core.Events.Commands;
using RmClient.Core.Events.Tactical;
using RmClient.Core.State;

namespace RmClient.Core.Commands;

/// <summary>
/// Local demo command validator and executor.
/// Validates all preconditions atomically before mutating state and emits CommandFeedbackEvent.
/// </summary>
public sealed class TerminalCommands
{
    public const int Ammo17UnitCost = 1;
    public const int Ammo42UnitCost = 10;
    public const int MaxPurchaseAmount = 500;
    public const int MaxAmmo = 1000;
    public const int RemoteAmmoAmount = 50;
    public const int RemoteHealthAmount = 100;
    public const float RemoteEnergyAmount = 25f;
    public const float MaxChassisEnergy = 100f;
    public const int TeamReceiver = -1;

    private static readonly Dictionary<RemoteExchangeKind, int> RemoteCosts = new()
    {
        [RemoteExchangeKind.Ammo] = 100,
        [RemoteExchangeKind.Health] = 200,
        [RemoteExchangeKind.AirSupport] = 300,
        [RemoteExchangeKind.Energy] = 100,
    };

    private readonly TerminalStore _store;
    private readonly IEventBus _bus;

    public TerminalCommands(TerminalStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bus = store.Bus;
    }

    public static int GetPurchaseCost(int amount, int caliber)
    {
        if (amount <= 0 || amount > MaxPurchaseAmount)
        {
            return -1;
        }
        return caliber switch
        {
            17 => amount * Ammo17UnitCost,
            42 => amount * Ammo42UnitCost,
            _ => -1,
        };
    }

    public static int GetRemoteCost(RemoteExchangeKind kind)
    {
        return RemoteCosts.TryGetValue(kind, out var cost) ? cost : -1;
    }

    public static int GetRemoteCost(string? kindCode)
    {
        return DomainLabels.TryParseExchangeKind(kindCode, out var kind) ? GetRemoteCost(kind) : -1;
    }

    public bool PurchaseAmmo(int amount, int caliber)
    {
        var cost = GetPurchaseCost(amount, caliber);
        if (cost < 0)
        {
            return Reject($"弹量须在 1..{MaxPurchaseAmount}，口径仅支持 17 或 42");
        }
        var robot = _store.State.GetActiveRobot();
        if (robot is null)
        {
            return Reject("尚未选择有效机器人");
        }
        if (robot.Ammo < 0 || robot.Ammo > MaxAmmo - amount)
        {
            return Reject($"补弹将超过演示携弹上限 {MaxAmmo}");
        }
        if (_store.State.Credits < cost)
        {
            return Reject($"金币不足，需要 {cost}");
        }

        // Validate everything before mutating credits or ammo.
        _store.UpdateCredits(_store.State.Credits - cost);
        _store.UpdateRobotAmmo(_store.State.ActiveTeam, robot.Id, robot.Ammo + amount);
        _store.SetRobotMessage($"演示补弹 +{amount}");
        return Accept($"购买 {amount} 发 {caliber} mm 弹药，花费 {cost} 金币");
    }

    public bool RemoteExchange(string? kindCode)
    {
        if (!DomainLabels.TryParseExchangeKind(kindCode, out var kind))
        {
            return Reject("不支持的远程兑换类型");
        }
        return RemoteExchange(kind);
    }

    public bool RemoteExchange(RemoteExchangeKind kind)
    {
        var cost = GetRemoteCost(kind);
        if (cost < 0)
        {
            return Reject("不支持的远程兑换类型");
        }
        var robot = _store.State.GetActiveRobot();
        if (robot is null)
        {
            return Reject("尚未选择有效机器人");
        }

        switch (kind)
        {
            case RemoteExchangeKind.Ammo:
                if (robot.Ammo < 0 || robot.Ammo > MaxAmmo - RemoteAmmoAmount)
                {
                    return Reject("演示弹药空间不足，不能兑换");
                }
                break;
            case RemoteExchangeKind.Health:
                if (robot.MaxHealth <= 0 || robot.Health >= robot.MaxHealth)
                {
                    return Reject("演示血量已满，不能兑换");
                }
                break;
            case RemoteExchangeKind.Energy:
                if (!float.IsFinite(robot.ChassisEnergy) || robot.ChassisEnergy >= MaxChassisEnergy)
                {
                    return Reject("演示底盘能量已满或无效，不能兑换");
                }
                break;
            case RemoteExchangeKind.AirSupport:
                if (_store.State.Modules.TryGetValue("air_support", out var active) && active)
                {
                    return Reject("演示空中支援已经启用");
                }
                break;
        }

        if (_store.State.Credits < cost)
        {
            return Reject($"金币不足，需要 {cost}");
        }

        _store.UpdateCredits(_store.State.Credits - cost);
        switch (kind)
        {
            case RemoteExchangeKind.Ammo:
                _store.UpdateRobotAmmo(_store.State.ActiveTeam, robot.Id, robot.Ammo + RemoteAmmoAmount);
                break;
            case RemoteExchangeKind.Health:
                var nextHealth = Math.Min(robot.MaxHealth, robot.Health + RemoteHealthAmount);
                _store.UpdateRobotHealth(_store.State.ActiveTeam, robot.Id, nextHealth);
                break;
            case RemoteExchangeKind.Energy:
                var nextEnergy = Math.Min(MaxChassisEnergy, robot.ChassisEnergy + RemoteEnergyAmount);
                _store.UpdateRobotEnergy(_store.State.ActiveTeam, robot.Id, nextEnergy);
                break;
            case RemoteExchangeKind.AirSupport:
                _store.SetModuleStatus("air_support", true);
                break;
        }

        var kindCode = kind.ToCode();
        _store.SetRobotMessage($"演示远程兑换：{kindCode}");
        return Accept($"远程兑换 {kindCode}，花费 {cost} 金币");
    }

    public bool SubmitMapMarker(NormalizedPoint position, string? kindCode, int receiverId)
    {
        if (!position.IsFinite)
        {
            return Reject("地图坐标必须是有限数");
        }
        if (!position.IsInBounds)
        {
            return Reject("地图坐标必须在 0..1 内");
        }
        if (!DomainLabels.TryParseMarkerKind(kindCode, out var kind))
        {
            return Reject("标记类型仅支持 attack、defend、alert");
        }
        return SubmitMapMarker(position, kind, receiverId);
    }

    public bool SubmitMapMarker(NormalizedPoint position, MapMarkerKind kind, int receiverId)
    {
        if (!position.IsFinite)
        {
            return Reject("地图坐标必须是有限数");
        }
        if (!position.IsInBounds)
        {
            return Reject("地图坐标必须在 0..1 内");
        }

        var team = _store.State.GetTeam(_store.State.ActiveTeam);
        if (receiverId != TeamReceiver && team.FindRobot(receiverId) is null)
        {
            return Reject("接收者必须是当前队机器人 ID 或 -1（演示全队）");
        }

        var receiverLabel = receiverId == TeamReceiver ? "全队" : $"{receiverId} 号机器人";
        var kindCode = kind.ToCode();
        _store.SetRobotMessage($"演示地图标记：{kindCode}");
        _bus.Publish(new MapMarkerSubmittedEvent(position, kind, receiverId));
        return Accept($"地图标记 {kindCode}，位置 ({position.X:F2}, {position.Y:F2})，接收者 {receiverLabel}");
    }

    /// <summary>
    /// Selects a local simulated robot; does not perform real network authentication.
    /// </summary>
    public bool SelectLogin(int robotId, string? teamCode)
    {
        if (!DomainLabels.TryParseTeamSide(teamCode, out var side))
        {
            return Reject("无效队伍或机器人 ID");
        }
        return SelectLogin(robotId, side);
    }

    public bool SelectLogin(int robotId, TeamSide side)
    {
        var team = _store.State.GetTeam(side);
        if (team.FindRobot(robotId) is null)
        {
            return Reject("无效队伍或机器人 ID");
        }
        _store.SelectRobot(robotId, side);
        _store.SetRobotMessage($"演示选择：{team.Title} {robotId} 号");
        return Accept($"选择 {team.Title} {robotId} 号机器人（并非真实登录）");
    }

    public bool TriggerDeployment()
    {
        _store.SetModuleStatus("deployment", true);
        _store.SetRobotMessage("演示部署已记录");
        return Accept("部署请求已记录到本地");
    }

    public bool TriggerRune()
    {
        _store.SetModuleStatus("rune", true);
        _store.SetRobotMessage("演示能量机关已激活");
        return Accept("模拟激活能量机关");
    }

    public bool ResetDemo()
    {
        _store.ResetDemo();
        return Accept("已重置演示数据");
    }

    private bool Accept(string message)
    {
        var formatted = $"演示：{message}（未发送到网络）";
        _store.AppendSystemEvent(formatted);
        _bus.Publish(new CommandFeedbackEvent(formatted, true));
        return true;
    }

    private bool Reject(string message)
    {
        // Failure emits feedback only: no charges, partial state changes, or event log churn.
        _bus.Publish(new CommandFeedbackEvent($"演示操作未执行：{message}", false));
        return false;
    }
}
