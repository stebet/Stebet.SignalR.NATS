namespace Stebet.SignalR.NATS;

internal static class HubConnectionContextExtensions
{
    public static bool IsInGroup(this HubConnectionContext connection, string groupName) =>
        connection.Items.TryGetValue("Groups", out object? groupObject) && groupObject is HashSet<string> groups && groups.Contains(groupName);

    public static async Task AddToGroupAsync(this HubConnectionContext connection, string groupName)
    {
        if (!connection.IsInGroup(groupName))
        {
            await connection.GetConnectionHandler().SubscribeToGroupAsync(groupName);
            if (connection.Items.TryGetValue("Groups", out object? groupObject) && groupObject is HashSet<string> groups)
            {
                groups.Add(groupName);
            }
            else
            {
                connection.Items["Groups"] = new HashSet<string> { groupName };
            }
        }
    }

    public static async Task RemoveFromGroupAsync(this HubConnectionContext connection, string groupName)
    {
        if (connection.Items.TryGetValue("Groups", out object? groupObject) && groupObject is HashSet<string> groups && groups.Remove(groupName))
        {
            await connection.GetConnectionHandler().UnsubscribeFromGroupAsync(groupName);
        }
    }

    public static void SetCancellationTokenSource(this HubConnectionContext connection, CancellationTokenSource cancellationToken) =>
        connection.Items["CancellationToken"] = cancellationToken;

    public static CancellationTokenSource GetCancellationTokenSource(this HubConnectionContext connection)
    {
        return (connection.Items.TryGetValue("CancellationToken", out object? cancellationToken) &&
                cancellationToken is CancellationTokenSource token)
            ? token
            : throw new InvalidOperationException("No CancellationTokenSource set on the connection.");
    }

    public static void SetConnectionHandler(this HubConnectionContext connection, NatsHubConnectionHandler handler)
    {
        connection.Items["ConnectionHandler"] = handler;
    }

    public static NatsHubConnectionHandler GetConnectionHandler(this HubConnectionContext connection)
    {
        return (connection.Items.TryGetValue("ConnectionHandler", out object? handler) &&
                handler is NatsHubConnectionHandler connectionHandler)
            ? connectionHandler
            : throw new InvalidOperationException("No ConnectionHandler set on the connection.");
    }
}
