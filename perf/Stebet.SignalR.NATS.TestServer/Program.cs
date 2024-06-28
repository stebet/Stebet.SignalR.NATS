using NATS.Client.Core;
using System.Threading.Channels;

using Stebet.SignalR.NATS.Tests;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
if (Environment.GetEnvironmentVariable("NATS_ENABLED") == "1")
{
    builder.Services
    .AddSignalR()
    .AddNats(Environment.GetEnvironmentVariable("NATS_HOST") ?? $"localhost:4222");
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
