namespace Stebet.SignalR.NATS.Tests
{
    public class EchoHub : Hub
    {
        private readonly ILogger<EchoHub> _logger;

        public EchoHub(ILogger<EchoHub> logger)
        {
            _logger = logger;
        }
        public Task<string> Send(string message)
        {
            return Task.FromResult(message);
        }

        public Task<string> SendToClient(string message, string connectionId)
        {
            return Clients.Client(connectionId).InvokeAsync<string>("Hello", message, Context.ConnectionId, CancellationToken.None);
        }

        public Task SendToAllClients(string message)
        {
            return Clients.All.SendAsync("Send", message);
        }

        public Task SendToOthers(string message)
        {
            return Clients.Others.SendAsync("Send", message);
        }
    }
}
