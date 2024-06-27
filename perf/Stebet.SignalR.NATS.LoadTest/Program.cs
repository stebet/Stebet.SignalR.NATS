using System.Diagnostics;

using Microsoft.AspNetCore.SignalR.Client;


List<HubConnection> _connections = new();
string message = $"Hello World";
await InitializeAsync();
Console.WriteLine("Sending some messages as a warm-up");
await Parallel.ForAsync(0L, 100000, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024 }, async (_, _) =>
{
    HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
    HubConnection conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
    string response = await conn2.InvokeAsync<string>("SendToClient", message, conn1.ConnectionId);
});

var timer = Stopwatch.StartNew();
using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
int responsesReceived = 0;
Console.WriteLine("Starting test");
try
{
    await Parallel.ForAsync(0L, Int64.MaxValue, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024, CancellationToken = cts.Token }, async (_, _) =>
    {
        HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
        HubConnection conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
        string response = await conn2.InvokeAsync<string>("SendToClient", message, conn1.ConnectionId);
        responsesReceived++;
    });
}
catch (TaskCanceledException)
{
    Console.WriteLine($"Received {responsesReceived:N0} responses in {timer.ElapsedMilliseconds:N0} (ms), {responsesReceived / (double)timer.Elapsed.TotalSeconds:N0} rps");
}

await CleanupAsync();


static async Task<HubConnection> GetConnection()
{
    HubConnection connection = new HubConnectionBuilder()
                .WithUrl($"http://localhost:5000/echo")
                .Build();
    await connection.StartAsync();
    return connection;
}

async Task InitializeAsync()
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

async Task CleanupAsync()
{
    await Parallel.ForEachAsync(_connections, async (connection, _) =>
    {
        await connection.DisposeAsync();
    });
}
