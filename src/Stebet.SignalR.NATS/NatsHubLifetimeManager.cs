using System.Diagnostics.CodeAnalysis;

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

    public NatsHubLifetimeManager(IOptions<NatsBackplaneOptions> options, ILogger<NatsHubLifetimeManager<THub>> logger,
        INatsConnection natsConnection, IHubProtocolResolver protocolResolver, IOptions<HubOptions> globalHubOptions,
        IOptions<HubOptions<THub>> hubOptions)
    {
        NatsSubject.Prefix = options.Value.Prefix;
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
        _logger.LogDebug("Handling Invoke Result from {ConnectionId}", connectionId);
        if (TryDeserializeMessage(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
        {
            _logger.LogDebug("Successfully deserialized CompletionMessage {InvocationId} with result {Result}", completionMessage.InvocationId, completionMessage.Result);

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
        var handler = new NatsHubConnectionHandler(_logger, connection, _natsConnection, _clientResultsManager);
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
        string invocationId = GenerateInvocationId();
        var invocationMessage = new InvocationMessage(invocationId, methodName, args);
        HubConnectionContext? conn = _connections[connectionId];

        if (conn is not null)
        {
            // This is a local connection so let's go the Write and SetConnectionResultAsync dance through a TCS
            Task<T> task = _clientResultsManager.AddInvocation<T>(connectionId, invocationId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, conn.ConnectionAborted).Token);
            _logger.LogDebug("Invoking {Method} on local connection {ConnectionId}", methodName, connectionId);
            await conn.WriteAsync(new SerializedHubMessage(invocationMessage.SerializeMessage(_clientResultsManager.HubProtocols)), cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch
            {
                // ConnectionAborted will trigger a generic "Canceled" exception from the task, let's convert it into a more specific message.
                if (conn.ConnectionAborted.IsCancellationRequested == true)
                {
                    throw new IOException($"Connection '{connectionId}' disconnected.");
                }

                throw;
            }
        }
        else
        {
            // This is a remote connection so let's do a NATS request which should do the local connection dance on a remote server
            _logger.LogDebug("Invoking {Method} on remote connection {ConnectionId}", methodName, connectionId);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.WriteMessageWithInvocationId(invocationMessage, _clientResultsManager.HubProtocols, invocationId);
            try
            {
                NatsMsg<NatsMemoryOwner<byte>> result = await _natsConnection.RequestAsync<NatsBufferWriter<byte>, NatsMemoryOwner<byte>>(NatsSubject.GetConnectionInvokeSubject(connectionId), bufferWriter,
                        cancellationToken: cancellationToken);
                using NatsMemoryOwner<byte> memoryOwner = result.Data;
                (string receivedConnectionId, SerializedHubMessage serializedHubMessage) = memoryOwner.ReadSerializedHubMessageWithConnectionId();
                _logger.LogDebug("Handling Invoke Result from {ConnectionId}", connectionId);
                if (TryDeserializeMessage<T>(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
                {
                    _logger.LogDebug("Successfully deserialized CompletionMessage {InvocationId} with result {Result}", completionMessage.InvocationId, completionMessage.Result);
                    if (completionMessage.HasResult)
                    {
                        return (T)completionMessage.Result!;
                    }
                    else
                    {
                        throw new HubException(completionMessage.Error);
                    }
                }

                throw new HubException($"Unable to deserialize CompletionMessage for invocation {invocationId} on connection {connectionId}");
            }
            catch (NatsNoRespondersException nre)
            {
                throw new IOException($"Connection '{connectionId}' does not exist.", nre);
            }
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

    private bool TryDeserializeMessage<T>(SerializedHubMessage message, [NotNullWhen(true)] out HubMessage? hubMessage)
    {
        foreach (IHubProtocol protocol in _protocolResolver.AllProtocols)
        {
            try
            {
                var messagePayload = new ReadOnlySequence<byte>(message.GetSerializedMessage(protocol));
                if (protocol.TryParseMessage(ref messagePayload, new RemoteInvocationBinder<T>(), out hubMessage))
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

    public override bool TryGetReturnType(string invocationId, [NotNullWhen(true)] out Type? type)
    {
        return _clientResultsManager.TryGetType(invocationId, out type);
    }

    private static string GenerateInvocationId()
    {
        Span<byte> buffer = stackalloc byte[24];
        Random.Shared.NextBytes(buffer);
        return Convert.ToBase64String(buffer);
    }
}
