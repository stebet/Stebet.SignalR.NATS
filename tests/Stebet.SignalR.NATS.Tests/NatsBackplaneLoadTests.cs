﻿using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

using Xunit;
using Xunit.Abstractions;

namespace Stebet.SignalR.NATS.Tests;
public class NatsBackplaneLoadTests : IClassFixture<SignalRWebApplicationFactory>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<HubConnection> _connections = new();

    public NatsBackplaneLoadTests(SignalRWebApplicationFactory factory, ITestOutputHelper output)
    {
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
    //[Theory]
#pragma warning restore xUnit1004 // Test methods should not be skipped
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(100000)]
    [InlineData(1000000)]
    public async Task ParallelRemoteClientInvoke(int numInvokes)
    {
        //using HttpClient client = _factory.CreateClient();
        await Parallel.ForAsync(0, numInvokes, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024 }, async (_, _) =>
        {
            HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
            HubConnection conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
            while (conn2.ConnectionId == conn1.ConnectionId)
            {
                conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
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

    public async Task InitializeAsync()
    {
        for (int i = 0; i < 100; i++)
        {
            HubConnection conn = await GetConnection();
            conn.On<string, string, string>("Hello", (message, connectionId) =>
            {
                return $"{conn.ConnectionId} got {message} from {connectionId}";
            });

            _connections.Add(conn);
        }

        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        await Parallel.ForEachAsync(_connections, async (connection, _) =>
        {
            await connection.DisposeAsync();
        });
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
