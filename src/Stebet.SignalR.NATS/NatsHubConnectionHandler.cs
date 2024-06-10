using System.Collections.ObjectModel;
using System.Threading.Channels;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace Stebet.SignalR.NATS;

public class NatsHubConnectionHandler(ILogger logger, HubConnectionContext connection, INatsConnection natsConnection)
{
    private readonly List<Task> _backgroundTasks = new();
    private readonly List<INatsSub<NatsMemoryOwner<byte>>> _subs = new();
    private readonly Dictionary<string, INatsSub<NatsMemoryOwner<byte>>> _groupSubs = new();

    public async Task StartConnectionHandler()
    {
        var tasks = new List<Task>(5)
        {
            SubscribeToSubject(
                    NatsSubject.GetConnectionInvokeSubject(connection.ConnectionId), ProcessConnectionInvokeAndResultAsync),
            SubscribeToSubject(NatsSubject.AllConnectionsSendSubject, ProcessConnectionSendWithExcludedConnectionIdsAsync),
            SubscribeToSubject(NatsSubject.GetConnectionSendSubject(connection.ConnectionId), ProcessConnectionSendAsync),
            SubscribeToSubject(NatsSubject.GetConnectionGroupWildcardSubject(connection.ConnectionId), ProcessGroupOperationAsync),
            connection.UserIdentifier is not null
                ? SubscribeToSubject(NatsSubject.GetUserSendSubject(connection.UserIdentifier), ProcessConnectionSendAsync)
                : Task.CompletedTask
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await Task.Delay(10);
    }

    private async Task SubscribeToSubject(string subject, Func<ChannelReader<NatsMsg<NatsMemoryOwner<byte>>>, Task> msgHandler)
    {
        INatsSub<NatsMemoryOwner<byte>> natsSubscription = await natsConnection.SubscribeCoreAsync<NatsMemoryOwner<byte>>(subject,
                    cancellationToken: connection.ConnectionAborted).ConfigureAwait(false);
        _subs.Add(natsSubscription);
        _backgroundTasks.Add(msgHandler(natsSubscription.Msgs));
    }

    public async Task StopConnectionHandler()
    {
        foreach (INatsSub<NatsMemoryOwner<byte>> sub in _subs)
        {
            await sub.UnsubscribeAsync().ConfigureAwait(false);
        }

        foreach (INatsSub<NatsMemoryOwner<byte>> groupSub in _groupSubs.Values)
        {
            await groupSub.UnsubscribeAsync().ConfigureAwait(false);
        }

        foreach (Task task in _backgroundTasks)
        {
            await task.ConfigureAwait(false);
        }
    }

    public async Task SubscribeToGroupAsync(string groupName)
    {
        INatsSub<NatsMemoryOwner<byte>> groupSubscription = await natsConnection.SubscribeCoreAsync<NatsMemoryOwner<byte>>(
            $"signalr.nats.group.{groupName}.send",
            cancellationToken: connection.ConnectionAborted).ConfigureAwait(false);
        _groupSubs.Add(groupName, groupSubscription);
        _backgroundTasks.Add(ProcessGroupSendAsync(groupSubscription.Msgs));
        await Task.Delay(10).ConfigureAwait(false);
    }

    public async Task UnsubscribeFromGroupAsync(string groupName)
    {
        if (connection.IsInGroup(groupName) && _groupSubs.TryGetValue(groupName, out INatsSub<NatsMemoryOwner<byte>>? groupSubscription))
        {
            await groupSubscription.UnsubscribeAsync().ConfigureAwait(false);
            _groupSubs.Remove(groupName);
        }
    }

    private async Task ProcessConnectionInvokeAndResultAsync(ChannelReader<NatsMsg<NatsMemoryOwner<byte>>> channel)
    {
        await Parallel.ForEachAsync(channel.ReadAllAsync(CancellationToken.None),
            Helpers.DefaultParallelOptions, async (message, _) =>
            {
                try
                {
                    logger.LogDebug("Writing invocation on connection {ConnectionId}", connection.ConnectionId);
                    await SendHubMessage(message);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing message");
                }

            }).ConfigureAwait(false);
    }

    private async Task ProcessConnectionSendAsync(ChannelReader<NatsMsg<NatsMemoryOwner<byte>>> channel)
    {
        await Parallel.ForEachAsync(channel.ReadAllAsync(CancellationToken.None),
            Helpers.DefaultParallelOptions, async (message, _) =>
            {
                try
                {
                    logger.LogDebug("Sending message on connection {ConnectionId}", connection.ConnectionId);
                    await SendHubMessage(message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing message");
                }
            }).ConfigureAwait(false);
    }

    private async Task ProcessConnectionSendWithExcludedConnectionIdsAsync(ChannelReader<NatsMsg<NatsMemoryOwner<byte>>> channel)
    {
        await Parallel.ForEachAsync(channel.ReadAllAsync(CancellationToken.None),
            Helpers.DefaultParallelOptions, async (message, _) =>
            {
                try
                {
                    logger.LogDebug("Sending message on connection {ConnectionId}", connection.ConnectionId);
                    await SendHubMessageWithExcludedConnectionIds(message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing message");
                }
            }).ConfigureAwait(false);
    }

    private async Task ProcessGroupOperationAsync(ChannelReader<NatsMsg<NatsMemoryOwner<byte>>> channel)
    {
        await Parallel.ForEachAsync(channel.ReadAllAsync(CancellationToken.None),
            Helpers.DefaultParallelOptions, async (message, _) =>
            {
                try
                {
                    using NatsMemoryOwner<byte> buffer = message.Data;
                    string groupName = buffer.ReadString();
                    if (message.Subject.EndsWith(".add"))
                    {
                        await connection.AddToGroupAsync(groupName).ConfigureAwait(false);
                    }
                    else if (message.Subject.EndsWith(".remove"))
                    {
                        await connection.RemoveFromGroupAsync(groupName).ConfigureAwait(false);
                    }

                    await message.ReplyAsync(true, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing message");
                }
            }).ConfigureAwait(false);
    }

    private async Task ProcessGroupSendAsync(ChannelReader<NatsMsg<NatsMemoryOwner<byte>>> channel)
    {
        await Parallel.ForEachAsync(channel.ReadAllAsync(CancellationToken.None),
            Helpers.DefaultParallelOptions, async (message, _) =>
            {
                try
                {
                    using NatsMemoryOwner<byte> buffer = message.Data;
                    (string groupName, ReadOnlyCollection<string> excludedConnections, SerializedHubMessage serializedHubMessage) = buffer.ReadSerializedHubMessageWithExcludedConnectionIdsAndGroupName();
                    if (connection.IsInGroup(groupName) && !excludedConnections.Contains(connection.ConnectionId))
                    {
                        logger.LogDebug("Sending message for group {GroupName} on connection {ConnectionId}", groupName, connection.ConnectionId);
                        await connection.WriteAsync(serializedHubMessage, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing message");
                }
            }).ConfigureAwait(false);
    }

    private async Task SendHubMessage(NatsMsg<NatsMemoryOwner<byte>> message)
    {
        using NatsMemoryOwner<byte> buffer = message.Data;
        SerializedHubMessage serializedHubMessage = buffer.ReadSerializedHubMessage();
        await connection.WriteAsync(serializedHubMessage, CancellationToken.None).ConfigureAwait(false);
        if (message.ReplyTo is not null)
        {
            await message.ReplyAsync(true).ConfigureAwait(false);
        }
    }

    private async Task SendHubMessageWithExcludedConnectionIds(NatsMsg<NatsMemoryOwner<byte>> message)
    {
        using NatsMemoryOwner<byte> buffer = message.Data;
        (ReadOnlyCollection<string> excludedConnections, SerializedHubMessage serializedHubMessage) = buffer.ReadSerializedHubMessageWithExcludedConnectionIds();
        if (!excludedConnections.Contains(connection.ConnectionId))
        {
            await connection.WriteAsync(serializedHubMessage, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
