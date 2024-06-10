using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Stebet.SignalR.NATS.Tests;

public class NatsBackplane
{
    public NatsBackplane(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; set; }

    public ServiceProvider CreateBackplane()
    {
        IServiceCollection services = new ServiceCollection();
        foreach (ServiceDescriptor descriptor in Services)
        {
            services.Add(descriptor);
        }

        services.AddSingleton(typeof(HubLifetimeManager<>), typeof(NatsHubLifetimeManager<>));
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        return serviceProvider;
    }
}
