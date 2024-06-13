namespace Stebet.SignalR.NATS.Messages
{
    [MessagePackObject]
    internal readonly struct NatsSerializedMessage
    {
        [Key(0)]
        internal readonly string ProtocolName { get; init; }
        [Key(1)]
        internal readonly byte[] SerializedMessage { get; init; }
    }

    [MessagePackObject]
    internal readonly struct NatsMessage
    {
        [Key(0)]
        internal readonly NatsSerializedMessage[] Messages { get; init; }
    }

    [MessagePackObject]
    internal readonly struct NatsMessageWithConnectionId
    {
        [Key(0)]
        internal readonly NatsSerializedMessage[] Messages { get; init; }
        [Key(1)]
        internal readonly string ConnectionId { get; init; }
    }

    [MessagePackObject]
    internal readonly struct NatsMessageWithInvocationId
    {
        [Key(0)]
        internal readonly IReadOnlyList<NatsSerializedMessage> Messages { get; init; }
        [Key(1)]
        internal readonly string InvocationId { get; init; }
    }

    [MessagePackObject]
    internal readonly struct NatsMessageWithExcludedConnectionIds
    {
        [Key(0)]
        internal readonly NatsSerializedMessage[] Messages { get; init; }
        [Key(1)]
        internal readonly IReadOnlyList<string> ExcludedConnectionIds { get; init; }
    }
}
