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

    internal static void WriteMessageWithInvocationId(this IBufferWriter<byte> bufferWriter, InvocationMessage message, IReadOnlyList<IHubProtocol> protocols, string invocationId)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(invocationId);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteCompletionMessageWithConnectionId(this IBufferWriter<byte> bufferWriter, CompletionMessage message, IReadOnlyList<IHubProtocol> protocols, string connectionId)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(connectionId);
        writer.Flush();
        bufferWriter.WriteMessage(message, protocols);
        writer.Flush();
    }

    internal static void WriteCompletionMessageWithConnectionId(this IBufferWriter<byte> bufferWriter, CompletionMessage message, IHubProtocol protocol, string connectionId)
    {
        MessagePackWriter writer = new(bufferWriter);
        writer.Write(connectionId);
        writer.WriteArrayHeader(1);
        using var tempBuffer = new NatsBufferWriter<byte>();
        protocol.WriteMessage(message, tempBuffer);
        writer.Write(protocol.Name);
        writer.WriteBinHeader(tempBuffer.WrittenCount);
        writer.WriteRaw(tempBuffer.WrittenMemory.Span);
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
            using var tempBuffer = new NatsBufferWriter<byte>();
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
