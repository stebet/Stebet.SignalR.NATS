using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

using Xunit;
using Xunit.Abstractions;

namespace Stebet.SignalR.NATS.Tests;
public class NatsBackplaneLoadTests : IClassFixture<SignalRWebApplicationFactory>
{
    private readonly ITestOutputHelper _output;
    private readonly WebApplicationFactory<Program> _factory;

    public NatsBackplaneLoadTests(SignalRWebApplicationFactory factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    logging.AddXUnit(output);
                });
            });
        });
    }

#pragma warning disable xUnit1004 // Test methods should not be skipped
    [Theory(Skip="Just run when doing a load test")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
    [InlineData(100, 100)]
    [InlineData(100, 1000)]
    [InlineData(100, 10000)]
    [InlineData(100, 100000)]
    public async Task ParallelRemoteClientInvoke(int numConnections, int numInvokes)
    {
        //using HttpClient client = _factory.CreateClient();

        var connections = new List<HubConnection>(numConnections);
        for(int i = 0; i < numConnections; i++)
        {
            var conn = await GetConnection();
            conn.On<string, string, string>("Hello", (message, connectionId) =>
            {
                Console.WriteLine("Drasl");
                return $"{conn.ConnectionId} got {message} from {connectionId}";
            });

            connections.Add(conn);
        }

        await Parallel.ForAsync(0, numInvokes, new ParallelOptions { TaskScheduler = TaskScheduler.Default }, async (_, _) =>
        {
            HubConnection conn1 = connections[Random.Shared.Next(0, connections.Count)];
            HubConnection conn2 = connections[Random.Shared.Next(0, connections.Count)];
            while (conn2.ConnectionId == conn1.ConnectionId)
            {
                conn2 = connections[Random.Shared.Next(0, connections.Count)];
            }

            string message = $"Hello World";
            string expectedResponse = $"{conn1.ConnectionId} got {message} from {conn2.ConnectionId}";
            string response = await conn2.InvokeAsync<string>("SendToClient", message, conn1.ConnectionId);
            Assert.Equal(expectedResponse, response);
        });
    }

    private static async Task<HubConnection> GetConnection()
    {
        HubConnection connection = new HubConnectionBuilder()
                    .WithUrl($"http://localhost:5000/echo")
                    .Build();
        await connection.StartAsync();
        return connection;
    }
}

public class SignalRWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }
}
