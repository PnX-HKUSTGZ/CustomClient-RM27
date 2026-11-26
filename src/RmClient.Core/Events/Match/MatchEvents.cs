using RmClient.Core.State;

namespace RmClient.Core.Events.Match;

public sealed record MatchClockUpdatedEvent(int RoundIndex, int RemainingSeconds) : IDomainEvent;

public sealed record TeamScoreUpdatedEvent(TeamSide Side, int Score) : IDomainEvent;

public sealed record BaseHealthUpdatedEvent(
    TeamSide Side,
    int BaseHealth,
    int BaseMaxHealth,
    int OutpostHealth) : IDomainEvent;

public sealed record CreditsUpdatedEvent(int Credits) : IDomainEvent;

public sealed record DemoStateResetEvent() : IDomainEvent;
