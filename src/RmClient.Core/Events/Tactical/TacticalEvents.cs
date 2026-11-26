using RmClient.Core.State;

namespace RmClient.Core.Events.Tactical;

public sealed record MapMarkerSubmittedEvent(
    NormalizedPoint Position,
    MapMarkerKind Kind,
    int ReceiverId) : IDomainEvent;

public sealed record RobotMessageUpdatedEvent(string Message) : IDomainEvent;

public sealed record SystemEventAppendedEvent(string Message) : IDomainEvent;
