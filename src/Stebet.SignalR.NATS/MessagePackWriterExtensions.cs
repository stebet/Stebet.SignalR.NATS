using Microsoft.AspNetCore.SignalR;

namespace MessagePack;

public static class MessagePackWriterExtensions
{
    internal static void WriteSerializedMessages(this ref MessagePackWriter writer, IReadOnlyList<SerializedMessage> messages)
    {
        writer.WriteArrayHeader(messages.Count);
        foreach (SerializedMessage message in messages)
        {
            writer.WriteSerializedMessage(message);
        }
    }

    private static void WriteSerializedMessage(this ref MessagePackWriter writer, SerializedMessage message)
    {
        writer.Write(message.ProtocolName);
        writer.Write(message.Serialized.Span);
    }

    internal static void WriteStringArrayToBuffer<TList>(this ref MessagePackWriter writer, TList stringList)
        where TList : IReadOnlyList<string>
    {
        writer.WriteArrayHeader(stringList.Count);
        foreach (string str in stringList)
        {
            writer.Write(str);
        }
    }
}
