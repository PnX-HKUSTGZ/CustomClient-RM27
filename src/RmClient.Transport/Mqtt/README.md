# MQTT Transport (Reserved)

Reserved for the RM2027 MQTT client implementation (planned: MQTTnet).

Planned responsibilities:
- Manage broker connection, exponential-backoff reconnect, and QoS 1 subscriptions.
- Marshal decoded messages onto `IMainThreadDispatcher` before publishing domain events to `IEventBus`.
- Track per-topic reception timestamps so stale/missing telemetry is never shown as valid zero values.
