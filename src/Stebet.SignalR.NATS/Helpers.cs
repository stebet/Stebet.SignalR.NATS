namespace Stebet.SignalR.NATS;

internal static class Helpers
{
    internal static readonly ParallelOptions DefaultParallelOptions = new() { TaskScheduler = TaskScheduler.Default };
}
