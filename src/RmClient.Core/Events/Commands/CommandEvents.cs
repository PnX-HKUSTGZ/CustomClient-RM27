namespace RmClient.Core.Events.Commands;

public sealed record CommandFeedbackEvent(string Message, bool Accepted) : IDomainEvent;
