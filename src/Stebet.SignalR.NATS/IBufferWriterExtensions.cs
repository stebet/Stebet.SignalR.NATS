using Microsoft.AspNetCore.SignalR.Protocol;

namespace System.Buffers;

public static class IBufferWriterExtensions
{
    internal static void WriteMessage<T>(this IBufferWriter<byte> bufferWriter, T message, IReadOnlyList<IHubProtocol> protocols)
            where T : HubMessage
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.WriteSerializedMessages(message.SerializeMessage(protocols));
        writer.Flush();
    }

    internal static void WriteMessageWithConnectionId<T>(this IBufferWriter<byte> bufferWriter, T message, IReadOnlyList<IHubProtocol> hubProtocols, string connectionId)
        where T : HubMessage
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(connectionId);
        writer.WriteSerializedMessages(message.SerializeMessage(hubProtocols));
        writer.Flush();
    }

    internal static void WriteMessageWithInvocationId<T>(this IBufferWriter<byte> bufferWriter, T message, IReadOnlyList<IHubProtocol> hubProtocols, string invocationId)
        where T : HubMessage
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(invocationId);
        writer.WriteSerializedMessages(message.SerializeMessage(hubProtocols));
        writer.Flush();
    }

    internal static void WriteMessageWithExcludedConnectionIds<T>(this IBufferWriter<byte> bufferWriter, T message, IReadOnlyList<IHubProtocol> hubProtocols, IReadOnlyList<string> excludedConnectionIds)
        where T : HubMessage
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.WriteStringArrayToBuffer(excludedConnectionIds);
        writer.WriteSerializedMessages(message.SerializeMessage(hubProtocols));
        writer.Flush();
    }

    internal static void WriteMessageWithExcludedConnectionIdsAndGroupName<T>(this IBufferWriter<byte> bufferWriter, T message, IReadOnlyList<IHubProtocol> hubProtocols, string groupName, IReadOnlyList<string> excludedConnectionIds)
        where T : HubMessage
    {
        MessagePackWriter writer = new(bufferWriter);
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("Group name is missing");
        }

        writer.Write(groupName);
        writer.WriteStringArrayToBuffer(excludedConnectionIds);
        writer.WriteSerializedMessages(message.SerializeMessage(hubProtocols));
        writer.Flush();
    }

    internal static void Write(this IBufferWriter<byte> bufferWriter, string groupName)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(groupName);
        writer.Flush();
    }
}
