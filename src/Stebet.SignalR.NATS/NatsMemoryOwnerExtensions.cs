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
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new InvalidOperationException("Connection ID is missing");
        }

        SerializedHubMessage message = reader.ReadSerializedHubMessage();
        return (connectionId, message);
    }

    internal static (string InvocationId, SerializedHubMessage SerializedHubMessage) ReadSerializedHubMessageWithInvocationId(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        string? invocationId = reader.ReadString();
        if (string.IsNullOrEmpty(invocationId))
        {
            throw new InvalidOperationException("Connection ID is missing");
        }

        SerializedHubMessage message = reader.ReadSerializedHubMessage();
        return (invocationId, message);
    }

    internal static string ReadString(this NatsMemoryOwner<byte> memory)
    {
        MessagePackReader reader = new(memory.Memory);
        string? stringVal = reader.ReadString();
        if (string.IsNullOrEmpty(stringVal))
        {
            throw new InvalidOperationException("String is missing");
        }

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
        if (string.IsNullOrEmpty(groupName))
        {
            throw new InvalidOperationException("Group name is missing");
        }

        ReadOnlyCollection<string> excludedConnections = messageData.ReadStringArray();
        SerializedHubMessage serializedHubMessage = messageData.ReadSerializedHubMessage();
        return (groupName, excludedConnections, serializedHubMessage);
    }
}
