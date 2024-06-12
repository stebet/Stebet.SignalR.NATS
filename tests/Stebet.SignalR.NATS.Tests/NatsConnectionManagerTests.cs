using System.Threading.Channels;

using NATS.Client.Core;

using Xunit.Abstractions;

namespace Stebet.SignalR.NATS.Tests;

public class NatsConnectionManagerTests : Microsoft.AspNetCore.SignalR.Specification.Tests.ScaleoutHubLifetimeManagerTests<NatsBackplane>
{
    IServiceCollection services = new ServiceCollection();
    public NatsConnectionManagerTests(ITestOutputHelper output) : base()
    {
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXUnit(output,  options =>
        {
            options.Filter = (category, logLevel) => logLevel >= LogLevel.Debug;
            options.IncludeScopes = true;
        }));
        services.AddTransient(provider => NatsOpts.Default with
        {
            LoggerFactory = provider.GetRequiredService<ILoggerFactory>(),
            Url = $"localhost:4222",
            RequestTimeout = TimeSpan.FromSeconds(30),
            SubPendingChannelFullMode = BoundedChannelFullMode.Wait,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            Name = $"Stebet.SignalR.NATS.Tests"
        });

        services.AddSingleton<INatsConnectionPool, NatsConnectionPool>();
        services.AddTransient(provider => provider.GetRequiredService<INatsConnectionPool>().GetConnection());
        services.AddSignalR();
    }

    public override HubLifetimeManager<Hub> CreateNewHubLifetimeManager() => CreateNewHubLifetimeManager(CreateBackplane());
    public override NatsBackplane CreateBackplane() => new NatsBackplane(services);
    public override HubLifetimeManager<Hub> CreateNewHubLifetimeManager(NatsBackplane backplane) =>
        backplane.CreateBackplane().GetRequiredService<HubLifetimeManager<Hub>>();
}
