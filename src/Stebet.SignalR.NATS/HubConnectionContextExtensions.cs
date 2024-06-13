namespace Stebet.SignalR.NATS;

internal static class HubConnectionContextExtensions
{
    public static bool IsInGroup(this HubConnectionContext connection, string groupName) =>
        connection.Items.TryGetValue("Groups", out object? groupObject) && groupObject is HashSet<string> groups && groups.Contains(groupName);

    public static async Task AddToGroupAsync<THub>(this HubConnectionContext connection, string groupName) where THub : Hub
    {
        if (!connection.IsInGroup(groupName))
        {
            await connection.GetConnectionHandler<THub>().SubscribeToGroupAsync(groupName);
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

    public static async Task RemoveFromGroupAsync<THub>(this HubConnectionContext connection, string groupName) where THub : Hub
    {
        if (connection.Items.TryGetValue("Groups", out object? groupObject) && groupObject is HashSet<string> groups && groups.Remove(groupName))
        {
            await connection.GetConnectionHandler<THub>().UnsubscribeFromGroupAsync(groupName);
        }
    }

    public static void SetConnectionHandler<THub>(this HubConnectionContext connection, NatsHubConnectionHandler<THub> handler) where THub : Hub
    {
        connection.Items["ConnectionHandler"] = handler;
    }

    public static NatsHubConnectionHandler<THub> GetConnectionHandler<THub>(this HubConnectionContext connection) where THub : Hub
    {
        return (connection.Items.TryGetValue("ConnectionHandler", out object? handler) &&
                handler is NatsHubConnectionHandler<THub> connectionHandler)
            ? connectionHandler
            : throw new InvalidOperationException("No ConnectionHandler set on the connection.");
    }
}
