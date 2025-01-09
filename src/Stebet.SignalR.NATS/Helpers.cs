namespace Stebet.SignalR.NATS;

internal static class Helpers
{
    internal static readonly ParallelOptions s_defaultParallelOptions = new() { TaskScheduler = TaskScheduler.Default };
}
