using MessagePack;

using Microsoft.AspNetCore.SignalR.Protocol;

using NATS.Client.Core;

namespace System.Buffers;

public static class IBufferWriterExtensions
{
    internal static void WriteMessageWithExcludedConnectionIds(this IBufferWriter<byte> bufferWriter, HubMessage message, IReadOnlyList<IHubProtocol> protocols, IReadOnlyList<string> excludedConnectionIds)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.WriteStringArrayToBuffer(excludedConnectionIds);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteMessageWithInvocationId(this IBufferWriter<byte> bufferWriter, HubMessage message, IReadOnlyList<IHubProtocol> protocols, string invocationId)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(invocationId);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteMessageWithConnectionId(this IBufferWriter<byte> bufferWriter, HubMessage message, IReadOnlyList<IHubProtocol> protocols, string connectionId)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(connectionId);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteMessageWithExcludedConnectionIdsAndGroupName(this IBufferWriter<byte> bufferWriter, HubMessage message, IReadOnlyList<IHubProtocol> protocols, string groupName, IReadOnlyList<string> excludedConnectionIds)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName, nameof(groupName));
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(groupName);
        writer.WriteStringArrayToBuffer(excludedConnectionIds);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteMessage(this IBufferWriter<byte> bufferWriter, HubMessage message, IReadOnlyList<IHubProtocol> protocols)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.WriteArrayHeader(protocols.Count);
        foreach (var protocol in protocols)
        {
            using NatsBufferWriter<byte> tempBuffer = new NatsBufferWriter<byte>();
            protocol.WriteMessage(message, tempBuffer);
            writer.Write(protocol.Name);
            writer.WriteBinHeader(tempBuffer.WrittenCount);
            writer.WriteRaw(tempBuffer.WrittenMemory.Span);
        }

        writer.Flush();
    }

    internal static void Write(this IBufferWriter<byte> bufferWriter, string groupName)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(groupName);
        writer.Flush();
    }
}
