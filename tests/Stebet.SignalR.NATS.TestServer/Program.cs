using NATS.Client.Core;
using System.Threading.Channels;

using Stebet.SignalR.NATS.Tests;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddTransient(provider => NatsOpts.Default with
    {
        LoggerFactory = provider.GetRequiredService<ILoggerFactory>(),
        Url = $"localhost:4222",
        RequestTimeout = TimeSpan.FromSeconds(30),
        SubPendingChannelFullMode = BoundedChannelFullMode.Wait,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        Name = $"Stebet.SignalR.NATS.Tests"
    })
    .AddSingleton<INatsConnectionPool, NatsConnectionPool>()
    .AddTransient(provider => provider.GetRequiredService<INatsConnectionPool>().GetConnection())
    .AddSignalR()
    .AddNats();
WebApplication app = builder.Build();
app.UseWebSockets();
app.MapHub<EchoHub>("/echo");
app.Run();

public partial class Program { }
