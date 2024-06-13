using Microsoft.AspNetCore.SignalR.Protocol;

namespace MessagePack;

public static class MessagePackWriterExtensions
{
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
