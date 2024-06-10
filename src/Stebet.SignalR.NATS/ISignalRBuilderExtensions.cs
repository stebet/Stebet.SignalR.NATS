using Microsoft.Extensions.DependencyInjection;
using Stebet.SignalR.NATS;

namespace Microsoft.AspNetCore.SignalR;

public static class ISignalRBuilderExtensions
{
    public static ISignalRServerBuilder AddNats(this ISignalRServerBuilder builder)
    {
        builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(NatsHubLifetimeManager<>));
        return builder;
    }
}
