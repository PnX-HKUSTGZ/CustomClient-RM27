using RmClient.Core.Settings;

namespace RmClient.Core.Events.Runtime;

public sealed record ConnectionStatusChangedEvent(string Label, bool Connected) : IDomainEvent;

public sealed record ModuleStatusUpdatedEvent(string ModuleKey, bool Online) : IDomainEvent;

public sealed record VideoStatusUpdatedEvent(string Label, bool Live, string Error) : IDomainEvent;

public sealed record SettingsChangedEvent(UserSettingsData Settings) : IDomainEvent;

public sealed record PeripheralConnectionChangedEvent(string PeripheralId, bool Connected) : IDomainEvent;

public sealed record PeripheralInputReceivedEvent(string PeripheralId, string Channel, object? Value) : IDomainEvent;

public sealed record PeripheralFaultEvent(string PeripheralId, string Message) : IDomainEvent;
