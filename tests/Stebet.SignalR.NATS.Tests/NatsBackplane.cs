namespace Stebet.SignalR.NATS.Tests;

public class NatsBackplane(IServiceCollection services)
{
    public IServiceCollection Services { get; set; } = services;

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
