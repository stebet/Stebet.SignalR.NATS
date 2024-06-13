using Microsoft.Extensions.DependencyInjection;
using Stebet.SignalR.NATS;

namespace Microsoft.AspNetCore.SignalR;

public static class ISignalRBuilderExtensions
{
    public static ISignalRServerBuilder AddNats(this ISignalRServerBuilder builder, string natsSubjectPrefix = "signalr.nats")
    {
        NatsSubject.Prefix = natsSubjectPrefix;
        builder.Services.AddSingleton(typeof(ClientResultsManager<>), typeof(ClientResultsManager<>));
        builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(NatsHubLifetimeManager<>));
        return builder;
    }
}
