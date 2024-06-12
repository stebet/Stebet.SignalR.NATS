namespace Stebet.SignalR.NATS.Tests
{
    public class EchoHub : Hub
    {
        private readonly ILogger<EchoHub> _logger;

        public EchoHub(ILogger<EchoHub> logger) {
            _logger = logger;
        }
        public Task<string> Send(string message)
        {
            return Task.FromResult(message);
        }

        public async Task<string> SendToClient(string message, string connectionId)
        {
            var response = await Clients.Client(connectionId).InvokeAsync<string>("Hello", message, Context.ConnectionId, CancellationToken.None); ;
            _logger.LogInformation("Received response {response}", response);
            return response;
        }
    }
}
