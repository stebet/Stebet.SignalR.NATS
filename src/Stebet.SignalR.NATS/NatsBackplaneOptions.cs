using Microsoft.Extensions.Options;

namespace Stebet.SignalR.NATS
{
    public class NatsBackplaneOptions : IOptions<NatsBackplaneOptions>
    {
        public string Prefix { get; set; } = "signalr.nats";
        public NatsBackplaneOptions Value => this;
    }
}
