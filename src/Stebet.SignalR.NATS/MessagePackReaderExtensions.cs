using System.Buffers;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.SignalR;

namespace MessagePack;

internal static class MessagePackReaderExtensions
{
    private static ReadOnlyCollection<SerializedMessage> ReadSerializedMessages(this ref MessagePackReader buffer)
    {
        int messageCount = buffer.ReadArrayHeader();
        var messages = new List<SerializedMessage>(messageCount);
        for (int i = 0; i < messageCount; i++)
        {
            messages.Add(buffer.ReadSerializedMessage());
        }

        return messages.AsReadOnly();
    }

    private static SerializedMessage ReadSerializedMessage(this ref MessagePackReader reader)
    {
        // Let's read the protocol name
        string? protocolName = reader.ReadString();
        if (string.IsNullOrEmpty(protocolName))
        {
            throw new InvalidOperationException("Protocol name is missing");
        }

        ReadOnlySequence<byte>? bytes = reader.ReadBytes();
        return new SerializedMessage(protocolName, bytes.HasValue ? bytes.Value.ToArray() : []);
    }

    internal static ReadOnlyCollection<string> ReadStringArray(this ref MessagePackReader reader)
    {
        int stringCount = reader.ReadArrayHeader();
        var strings = new List<string>(stringCount);
        for (int i = 0; i < stringCount; i++)
        {
            string? readString = reader.ReadString();
            if (string.IsNullOrEmpty(readString))
            {
                throw new InvalidOperationException("String is missing");
            }

            strings.Add(readString);
        }

        return strings.AsReadOnly();
    }

    internal static SerializedHubMessage ReadSerializedHubMessage(this ref MessagePackReader reader)
    {
        return new SerializedHubMessage(reader.ReadSerializedMessages());
    }
}
