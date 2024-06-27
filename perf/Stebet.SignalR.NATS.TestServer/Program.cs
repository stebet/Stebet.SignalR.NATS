using NATS.Client.Core;
using System.Threading.Channels;

using Stebet.SignalR.NATS.Tests;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
if (Environment.GetEnvironmentVariable("NATS_ENABLED") == "1")
{
    builder.Services
    .AddTransient(provider => NatsOpts.Default with
    {
        LoggerFactory = provider.GetRequiredService<ILoggerFactory>(),
        Url = Environment.GetEnvironmentVariable("NATS_HOST") ?? $"localhost:4222",
        RequestTimeout = TimeSpan.FromSeconds(30),
        SubPendingChannelFullMode = BoundedChannelFullMode.Wait,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        Name = $"Stebet.SignalR.NATS.Tests"
    })
    .AddSingleton<INatsConnectionPool, NatsConnectionPool>()
    .AddTransient(provider => provider.GetRequiredService<INatsConnectionPool>().GetConnection())
    .AddSignalR()
    .AddNats();
}
else if (Environment.GetEnvironmentVariable("REDIS_ENABLED") == "1")
{
    builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost");
}

WebApplication app = builder.Build();
app.UseWebSockets();
app.MapHub<EchoHub>("/echo");
app.Run();

public partial class Program { }
