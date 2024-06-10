namespace Microsoft.AspNetCore.SignalR.Protocol;

public static class HubMessageExtensions
{
    internal static IReadOnlyList<SerializedMessage> SerializeMessage<T>(this T message, IReadOnlyList<IHubProtocol> hubProtocols)
        where T : HubMessage
    {
        var list = new List<SerializedMessage>(hubProtocols.Count);
        for (int i = 0; i < hubProtocols.Count; i++)
        {
            IHubProtocol protocol = hubProtocols[i];
            var serializedMessage = new SerializedMessage(protocol.Name, protocol.GetMessageBytes(message));
            list.Add(serializedMessage);
        }

        return list;
    }


}
