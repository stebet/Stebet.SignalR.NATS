namespace Stebet.SignalR.NATS.Tests
{
    public class EchoHub() : Hub
    {
        public Task<string> Send(string message) => Task.FromResult(message);
        public Task<string> SendToClient(string message, string connectionId) => Clients.Client(connectionId).InvokeAsync<string>("Hello", message, Context.ConnectionId, CancellationToken.None);
        public Task SendToAllClients(string message) => Clients.All.SendAsync("Send", message);
        public Task SendToOthers(string message) => Clients.Others.SendAsync("Send", message);
    }
}
