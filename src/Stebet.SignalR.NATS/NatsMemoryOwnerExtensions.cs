using System.Collections.ObjectModel;

namespace NATS.Client.Core;

internal static class NatsMemoryOwnerExtensions
{
    internal static SerializedHubMessage ReadSerializedHubMessage(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        return reader.ReadSerializedHubMessage();
    }

    internal static (string ConnectionId, SerializedHubMessage SerializedHubMessage) ReadSerializedHubMessageWithConnectionId(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        string? connectionId = reader.ReadString();
        ArgumentException.ThrowIfNullOrEmpty(connectionId, nameof(connectionId));
        SerializedHubMessage message = reader.ReadSerializedHubMessage();
        return (connectionId, message);
    }

    internal static (string InvocationId, SerializedHubMessage SerializedHubMessage) ReadSerializedHubMessageWithInvocationId(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        string? invocationId = reader.ReadString();
        ArgumentException.ThrowIfNullOrEmpty(invocationId, nameof(invocationId));
        SerializedHubMessage message = reader.ReadSerializedHubMessage();
        return (invocationId, message);
    }

    internal static string ReadString(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        string? stringVal = reader.ReadString();
        ArgumentException.ThrowIfNullOrEmpty(stringVal, nameof(stringVal));
        return stringVal;
    }

    internal static (ReadOnlyCollection<string> ExcludedConnectionIds, SerializedHubMessage SerializedHubMessage) ReadSerializedHubMessageWithExcludedConnectionIds(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader messageData = new(memory.Memory);
        ReadOnlyCollection<string> excludedConnections = messageData.ReadStringArray();
        SerializedHubMessage serializedHubMessage = messageData.ReadSerializedHubMessage();
        return (excludedConnections, serializedHubMessage);
    }

    internal static (string GroupName, ReadOnlyCollection<string> ExcludedConnectionIds, SerializedHubMessage SerializedHubMessage) ReadSerializedHubMessageWithExcludedConnectionIdsAndGroupName(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader messageData = new(memory.Memory);
        string? groupName = messageData.ReadString();
        ArgumentException.ThrowIfNullOrEmpty(groupName, nameof(groupName));
        ReadOnlyCollection<string> excludedConnections = messageData.ReadStringArray();
        SerializedHubMessage serializedHubMessage = messageData.ReadSerializedHubMessage();
        return (groupName, excludedConnections, serializedHubMessage);
    }
}
