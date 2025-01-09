using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

using Stebet.SignalR.NATS;

namespace Microsoft.AspNetCore.SignalR;

public static class ISignalRBuilderExtensions
{
    [RequiresUnreferencedCode("MessagePack does not support trimming currently.")]
    public static ISignalRServerBuilder AddNats(this ISignalRServerBuilder builder, string natsUrl, string natsSubjectPrefix = "signalr.nats")
    {
        builder.AddMessagePackProtocol();
        NatsSubject.Prefix = natsSubjectPrefix;
        builder.Services.AddKeyedSingleton<INatsConnectionPool>("Stebet.SignalR.NATS", (provider, key) =>
        {
            var pool = new NatsConnectionPool(NatsOpts.Default with
            {
                LoggerFactory = provider.GetRequiredService<ILoggerFactory>(),
                Url = natsUrl,
                RequestTimeout = TimeSpan.FromSeconds(30),
                SubPendingChannelFullMode = BoundedChannelFullMode.Wait,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                Name = $"Stebet.SignalR.NATS.Tests"
            });

            return pool;
        });
        builder.Services.AddSingleton(typeof(ClientResultsManager<>), typeof(ClientResultsManager<>));
        builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(NatsHubLifetimeManager<>));
        return builder;
    }
}
