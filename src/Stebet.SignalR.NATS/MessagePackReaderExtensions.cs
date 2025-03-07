﻿using System.Collections.ObjectModel;

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
        ArgumentException.ThrowIfNullOrEmpty(protocolName, nameof(protocolName));
        ReadOnlySequence<byte>? bytes = reader.ReadBytes();
        return new SerializedMessage(protocolName, bytes.HasValue ? bytes.Value.ToArray() : []);
    }

    internal static SortedSet<string> ReadStringArray(this ref MessagePackReader reader)
    {
        int stringCount = reader.ReadArrayHeader();
        var strings = new SortedSet<string>();
        for (int i = 0; i < stringCount; i++)
        {
            string? readString = reader.ReadString();
            ArgumentException.ThrowIfNullOrEmpty(readString, nameof(readString));
            strings.Add(readString);
        }

        return strings;
    }

    internal static SerializedHubMessage ReadSerializedHubMessage(this ref MessagePackReader reader) => new(reader.ReadSerializedMessages());
}
