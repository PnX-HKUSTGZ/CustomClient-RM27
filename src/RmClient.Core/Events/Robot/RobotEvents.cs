using RmClient.Core.State;

namespace RmClient.Core.Events.Robot;

public sealed record ActiveRobotChangedEvent(TeamSide Team, int RobotId) : IDomainEvent;

public sealed record RobotHealthUpdatedEvent(
    TeamSide Team,
    int RobotId,
    int Health,
    int MaxHealth,
    bool Online) : IDomainEvent;

public sealed record RobotAmmoUpdatedEvent(
    TeamSide Team,
    int RobotId,
    int Ammo,
    float MuzzleSpeed) : IDomainEvent;

public sealed record RobotHeatPowerUpdatedEvent(
    TeamSide Team,
    int RobotId,
    float Heat,
    float HeatLimit,
    float Power,
    float PowerLimit) : IDomainEvent;

public sealed record RobotEnergyUpdatedEvent(
    TeamSide Team,
    int RobotId,
    float ChassisEnergy,
    float BufferEnergy) : IDomainEvent;

public sealed record RobotPositionUpdatedEvent(
    TeamSide Team,
    int RobotId,
    NormalizedPoint Position) : IDomainEvent;
