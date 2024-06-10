using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace Stebet.SignalR.NATS;

public class NatsHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
{
    private readonly ILogger<NatsHubLifetimeManager<THub>> _logger;
    private readonly INatsConnection _natsConnection;
    private readonly Task _worker;
    private readonly IHubProtocolResolver _protocolResolver;
    private readonly ClientResultsManager _clientResultsManager;
    private readonly HubConnectionStore _connections = new();

    public NatsHubLifetimeManager(ILogger<NatsHubLifetimeManager<THub>> logger,
        INatsConnection natsConnection, IHubProtocolResolver protocolResolver, IOptions<HubOptions> globalHubOptions,
        IOptions<HubOptions<THub>> hubOptions)
    {
        _logger = logger;
        _natsConnection = natsConnection;
        _protocolResolver = protocolResolver;
        _clientResultsManager =
            new ClientResultsManager(protocolResolver, globalHubOptions.Value.SupportedProtocols, hubOptions.Value.SupportedProtocols);
        INatsSub<NatsMemoryOwner<byte>> connSub = _natsConnection.SubscribeCoreAsync<NatsMemoryOwner<byte>>($"{NatsSubject.GlobalSubjectPrefix}.>").AsTask()
            .GetAwaiter().GetResult();
        _worker = StartSubscriptions(connSub);
    }

    private async Task StartSubscriptions(INatsSub<NatsMemoryOwner<byte>> sub)
    {
        await Parallel.ForEachAsync(sub.Msgs.ReadAllAsync(), Helpers.DefaultParallelOptions,
            async (message, token) =>
            {
                using NatsMemoryOwner<byte> messageDataOwner = message.Data;
                try
                {
                    _logger.LogDebug("Received message {Subject}", message.Subject);
                    if (message.Subject == NatsSubject.ConnectionDisconnectedSubject)
                    {
                        HandleConnectionDisconnected(messageDataOwner);
                    }
                    else if (message.Subject == NatsSubject.InvokeResultSubject)
                    {
                        HandleInvokeResult(messageDataOwner);
                        await message.ReplyAsync(true, cancellationToken: token).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error processing message {Subject}", message.Subject);
                }
            }).ConfigureAwait(false);
    }

    private void HandleInvokeResult(NatsMemoryOwner<byte> memory)
    {
        (string connectionId, SerializedHubMessage serializedHubMessage) = memory.ReadSerializedHubMessageWithConnectionId();
        if (TryDeserializeMessage(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
        {
            _clientResultsManager.TryCompleteResult(connectionId, completionMessage);
        }
        else
        {
            _logger.LogWarning("Failed to deserialize completion message");
        }
    }

    private void HandleConnectionDisconnected(NatsMemoryOwner<byte> memory)
    {
        string connectionId = memory.ReadString();
        if (_connections[connectionId] is null)
        {
            _clientResultsManager.AbortInvocationsForConnection(connectionId);
        }
    }

    public override async Task OnConnectedAsync(HubConnectionContext connection)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionAborted);
        connection.SetCancellationTokenSource(cts);
        var handler = new NatsHubConnectionHandler(_logger, connection, _natsConnection);
        await handler.StartConnectionHandler().ConfigureAwait(false);
        connection.SetConnectionHandler(handler);
        _connections.Add(connection);
    }

    public override async Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        await connection.GetConnectionHandler().StopConnectionHandler().ConfigureAwait(false);
        await connection.GetCancellationTokenSource().CancelAsync().ConfigureAwait(false);
        _connections.Remove(connection);
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.Write(connection.ConnectionId);
        await _natsConnection.PublishAsync(NatsSubject.ConnectionDisconnectedSubject, bufferWriter).ConfigureAwait(false);
    }

    public override Task SendAllAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to everyone", methodName);
        return SendAllExceptAsync(methodName, args, [], cancellationToken);
    }

    public override async Task SendAllExceptAsync(string methodName, object?[] args, IReadOnlyList<string> excludedConnectionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to everyone except connections {ExcludedConnectionIds}", methodName,
            string.Join(',', excludedConnectionIds));
        var invocationMessage = new InvocationMessage(methodName, args);
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessageWithExcludedConnectionIds(invocationMessage, _clientResultsManager.HubProtocols, excludedConnectionIds);
        await _natsConnection.PublishAsync(NatsSubject.AllConnectionsSendSubject, bufferWriter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public override Task SendConnectionAsync(string connectionId, string methodName, object?[] args,
        CancellationToken cancellationToken = default)
    {
        var invocationMessage = new InvocationMessage(methodName, args);
        return SendConnectionAsyncImpl(connectionId, invocationMessage, cancellationToken);
    }

    private Task SendConnectionAsyncImpl(string connectionId, InvocationMessage invocationMessage, CancellationToken cancellationToken)
    {
        HubConnectionContext? conn = _connections[connectionId];
        return conn is not null
            ? conn.WriteAsync(invocationMessage, cancellationToken).AsTask()
            : SendMessageToSubject(NatsSubject.GetConnectionSendSubject(connectionId), cancellationToken, invocationMessage);
    }

    public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object?[] args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to connections {ConnectionIds}", methodName, string.Join(',', connectionIds));
        var tasks = new List<Task>(connectionIds.Count);
        var invocationMessage = new InvocationMessage(methodName, args);
        foreach (string connectionId in connectionIds)
        {
            tasks.Add(SendConnectionAsyncImpl(connectionId, invocationMessage, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public override Task SendGroupAsync(string groupName, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendGroupsAsync(new[] { groupName }, methodName, args, cancellationToken);
    }

    public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object?[] args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to groups {Groups}", methodName, string.Join(',', groupNames));
        var invocationMessage = new InvocationMessage(methodName, args);
        var tasks = new List<Task>(groupNames.Count);
        foreach (string groupName in groupNames)
        {
            tasks.Add(SendGroupMessageToSubject(NatsSubject.GetGroupSendSubject(groupName), groupName, [], cancellationToken, invocationMessage));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public override async Task SendGroupExceptAsync(string groupName, string methodName, object?[] args, IReadOnlyList<string> excludedConnectionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to group {Group} but excluding {ConnectionIds}", methodName, groupName,
            string.Join(',', excludedConnectionIds));
        var invocationMessage = new InvocationMessage(methodName, args);
        await SendGroupMessageToSubject(NatsSubject.GetGroupSendSubject(groupName), groupName, excludedConnectionIds, cancellationToken, invocationMessage)
            .ConfigureAwait(false);
    }

    public override Task SendUserAsync(string userId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        var invocationMessage = new InvocationMessage(methodName, args);
        return SendMessageToSubject(NatsSubject.GetUserSendSubject(userId), cancellationToken, invocationMessage);
    }

    public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object?[] args,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending {Method} to users {Users}", methodName, string.Join(',', userIds));
        var invocationMessage = new InvocationMessage(methodName, args);
        var tasks = new List<Task>(userIds.Count);
        foreach (string userId in userIds)
        {
            tasks.Add(SendMessageToSubject(NatsSubject.GetUserSendSubject(userId), cancellationToken, invocationMessage));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public override async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        HubConnectionContext? conn = _connections[connectionId];
        if (conn is not null)
        {
            _logger.LogDebug("Adding local connection {ConnectionId} to group {Group}", connectionId, groupName);
            await conn.AddToGroupAsync(groupName);
        }
        else
        {
            _logger.LogDebug("Adding remote connection {ConnectionId} to group {Group}", connectionId, groupName);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.Write(groupName);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.GetConnectionGroupAddSubject(connectionId), bufferWriter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Added remote connection {ConnectionId} to group {Group}", connectionId, groupName);
        }
    }

    public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        HubConnectionContext? conn = _connections[connectionId];
        if (conn is not null)
        {
            _logger.LogDebug("Removing local connection {ConnectionId} from group {Group}", connectionId, groupName);
            await conn.RemoveFromGroupAsync(groupName).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Removing remote connection {ConnectionId} from group {Group}", connectionId, groupName);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.Write(groupName);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.GetConnectionGroupRemoveSubject(connectionId), bufferWriter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Removed remote connection {ConnectionId} from group {Group}", connectionId, groupName);
        }
    }

    public override async Task<T> InvokeConnectionAsync<T>(string connectionId, string methodName, object?[] args,
        CancellationToken cancellationToken)
    {
        string invocationId = Guid.NewGuid().ToString();
        var invocationMessage = new InvocationMessage(invocationId, methodName, args);
        HubConnectionContext? conn = _connections[connectionId];
        Task<T> task = _clientResultsManager.AddInvocation<T>(connectionId, invocationId,
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, conn?.ConnectionAborted ?? default).Token);

        if (conn is not null)
        {
            _logger.LogDebug("Invoking {Method} on local connection {ConnectionId}", methodName, connectionId);
            await conn.WriteAsync(new SerializedHubMessage(invocationMessage.SerializeMessage(_clientResultsManager.HubProtocols)), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Invoking {Method} on remote connection {ConnectionId}", methodName, connectionId);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.WriteMessage(invocationMessage, _clientResultsManager.HubProtocols);
            try
            {
                await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.GetConnectionInvokeSubject(connectionId), bufferWriter,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (NatsNoRespondersException nre)
            {
                _clientResultsManager.RemoveInvocation(invocationId);
                throw new IOException($"Connection '{connectionId}' does not exist.", nre);
            }
            catch
            {
                _clientResultsManager.RemoveInvocation(invocationId);
            }
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            // ConnectionAborted will trigger a generic "Canceled" exception from the task, let's convert it into a more specific message.
            if (conn?.ConnectionAborted.IsCancellationRequested == true)
            {
                throw new IOException($"Connection '{connectionId}' disconnected.");
            }

            throw;
        }
    }

    public override async Task SetConnectionResultAsync(string connectionId, CompletionMessage result)
    {
        if (_clientResultsManager.HasInvocation(result.InvocationId!))
        {
            _logger.LogDebug("Setting result for local connection {ConnectionId}", connectionId);
            _clientResultsManager.TryCompleteResult(connectionId, result);
        }
        else
        {
            _logger.LogDebug("Setting result for remote connection {ConnectionId}", connectionId);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.WriteMessageWithConnectionId(result, _clientResultsManager.HubProtocols, connectionId);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.InvokeResultSubject, bufferWriter).ConfigureAwait(false);
        }
    }

    private async Task SendMessageToSubject<T>(string subject, CancellationToken cancellationToken,
        T invocationMessage) where T : HubMessage
    {
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessage(invocationMessage, _clientResultsManager.HubProtocols);
        await _natsConnection.PublishAsync(subject, bufferWriter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendGroupMessageToSubject<T>(string subject, string groupName, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken,
        T invocationMessage) where T : HubMessage
    {
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessageWithExcludedConnectionIdsAndGroupName(invocationMessage, _clientResultsManager.HubProtocols, groupName, excludedConnectionIds);
        await _natsConnection.PublishAsync(subject, bufferWriter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private bool TryDeserializeMessage(SerializedHubMessage message, [NotNullWhen(true)] out HubMessage? hubMessage)
    {
        foreach (IHubProtocol protocol in _protocolResolver.AllProtocols)
        {
            try
            {
                var messagePayload = new ReadOnlySequence<byte>(message.GetSerializedMessage(protocol));
                if (protocol.TryParseMessage(ref messagePayload, _clientResultsManager, out hubMessage))
                {
                    return true;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Can't deserialize message with protocol {Protocol}", protocol.Name);
            }
        }

        hubMessage = null;
        return false;
    }
}
