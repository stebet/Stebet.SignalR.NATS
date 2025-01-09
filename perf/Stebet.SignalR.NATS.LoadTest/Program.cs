using System.Diagnostics;

using Microsoft.AspNetCore.SignalR.Client;


List<HubConnection> _connections = [];
string message = $"Hello World";
await InitializeAsync();
Console.WriteLine("Sending some messages as a warm-up");
await Parallel.ForAsync(0L, 100000, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024 }, async (_, cancellationToken) =>
{
    HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
    HubConnection conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
    string response = await conn2.InvokeAsync<string>("SendToClient", message, conn1.ConnectionId, cancellationToken);
});

var timer = Stopwatch.StartNew();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
int responsesReceived = 0;
Console.WriteLine("Starting invoke test");
try
{
    await Parallel.ForAsync(0L, long.MaxValue, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024, CancellationToken = cts.Token }, async (_, cancellationToken) =>
    {
        HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
        HubConnection conn2 = _connections[Random.Shared.Next(0, _connections.Count)];
        string response = await conn2.InvokeAsync<string>("SendToClient", message, conn1.ConnectionId, cancellationToken);
        responsesReceived++;
    });
}
catch (TaskCanceledException)
{
    Console.WriteLine($"Received {responsesReceived:N0} responses in {timer.ElapsedMilliseconds:N0} (ms), {responsesReceived / (double)timer.Elapsed.TotalSeconds:N0} rps");
}

timer = Stopwatch.StartNew();
using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
responsesReceived = 0;
Console.WriteLine("Starting send all test");
try
{
    await Parallel.ForAsync(0L, long.MaxValue, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024, CancellationToken = cts2.Token }, async (_, cancellationToken) =>
    {
        HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
        await conn1.InvokeAsync("SendToAllClients", message, cancellationToken);
        responsesReceived++;
    });
}
catch (TaskCanceledException)
{
    Console.WriteLine($"Received {responsesReceived:N0} responses in {timer.ElapsedMilliseconds:N0} (ms), {responsesReceived / (double)timer.Elapsed.TotalSeconds:N0} rps");
}

timer = Stopwatch.StartNew();
using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
responsesReceived = 0;
Console.WriteLine("Starting send others test");
try
{
    await Parallel.ForAsync(0L, long.MaxValue, new ParallelOptions { TaskScheduler = TaskScheduler.Default, MaxDegreeOfParallelism = 1024, CancellationToken = cts3.Token }, async (_, cancellationToken) =>
    {
        HubConnection conn1 = _connections[Random.Shared.Next(0, _connections.Count)];
        await conn1.InvokeAsync("SendToOthers", message, cancellationToken);
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

        conn.On<string>("Send", (message) =>
        {
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
