using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace Stebet.SignalR.NATS;

internal partial class NatsHubLifetimeManager<THub>(ILogger<NatsHubLifetimeManager<THub>> logger, [FromKeyedServices("Stebet.SignalR.NATS")]INatsConnectionPool natsConnectionPool, IHubProtocolResolver protocolResolver, ClientResultsManager<THub> clientResultsManager) : HubLifetimeManager<THub> where THub : Hub
{
    private bool _started = false;
    private readonly HubConnectionStore _connections = new();
    private readonly INatsConnection _natsConnection = natsConnectionPool.GetConnection();

    public override async Task OnConnectedAsync(HubConnectionContext connection)
    {
        if (!_started)
        {
            var _ = Task.Run(StartSubscriptions);
            _started = true;
        }

        var handler = new NatsHubConnectionHandler<THub>(logger, connection, natsConnectionPool.GetConnection(), clientResultsManager);
        await handler.StartConnectionHandler().ConfigureAwait(false);
        connection.SetConnectionHandler(handler);
        _connections.Add(connection);
    }

    public override async Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        await connection.GetConnectionHandler<THub>().StopConnectionHandler().ConfigureAwait(false);
        _connections.Remove(connection);
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.Write(connection.ConnectionId);
        await _natsConnection.PublishAsync(NatsSubject.ConnectionDisconnectedSubject, bufferWriter).ConfigureAwait(false);
    }

    public override Task SendAllAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendAllExceptAsync(methodName, args, [], cancellationToken);
    }

    public override async Task SendAllExceptAsync(string methodName, object?[] args, IReadOnlyList<string> excludedConnectionIds,
        CancellationToken cancellationToken = default)
    {
        if(excludedConnectionIds.Count == 0)
        {
            LoggerMessages.Send(logger, methodName);
        }
        else
        {
            LoggerMessages.SendExcept(logger, methodName, excludedConnectionIds);
        }

        var invocationMessage = new InvocationMessage(methodName, args);
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessageWithExcludedConnectionIds(invocationMessage, clientResultsManager.HubProtocols, excludedConnectionIds);
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
        LoggerMessages.SendConnections(logger, methodName, connectionIds);
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
        LoggerMessages.SendGroups(logger, methodName, groupNames);
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
        LoggerMessages.SendGroupExcept(logger, methodName, groupName, excludedConnectionIds);
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
        LoggerMessages.SendUsers(logger, methodName, userIds);
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
            LoggerMessages.AddLocalConnectionToGroup(logger, connectionId, groupName);
            await conn.AddToGroupAsync<THub>(groupName);
        }
        else
        {
            LoggerMessages.AddRemoteConnectionToGroup(logger, connectionId, groupName);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.Write(groupName);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.GetConnectionGroupAddSubject(connectionId), bufferWriter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        HubConnectionContext? conn = _connections[connectionId];
        if (conn is not null)
        {
            LoggerMessages.RemoveLocalConnectionFromGroup(logger, connectionId, groupName);
            await conn.RemoveFromGroupAsync<THub>(groupName).ConfigureAwait(false);
        }
        else
        {
            LoggerMessages.RemoveRemoteConnectionFromGroup(logger, connectionId, groupName);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.Write(groupName);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.GetConnectionGroupRemoveSubject(connectionId), bufferWriter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public override async Task<T> InvokeConnectionAsync<T>(string connectionId, string methodName, object?[] args,
        CancellationToken cancellationToken)
    {
        string invocationId = Guid.NewGuid().ToString();
        var invocationMessage = new InvocationMessage(invocationId, methodName, args);
        HubConnectionContext? conn = _connections[connectionId];

        if (conn is not null)
        {
            // This is a local connection so let's go the Write and SetConnectionResultAsync dance through a TCS
            LoggerMessages.InvokeLocalConnection(logger, methodName, connectionId);
            Task<T> task = clientResultsManager.AddInvocation<T>(connectionId, invocationId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, conn.ConnectionAborted).Token);
            await conn.WriteAsync(invocationMessage, cancellationToken).ConfigureAwait(false);
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
            LoggerMessages.InvokeRemoteConnection(logger, methodName, connectionId);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.WriteMessageWithInvocationId(invocationMessage, clientResultsManager.HubProtocols, invocationId);
            try
            {
                NatsMsg<NatsMemoryOwner<byte>> result = await _natsConnection.RequestAsync<NatsBufferWriter<byte>, NatsMemoryOwner<byte>>(NatsSubject.GetConnectionInvokeSubject(connectionId), bufferWriter,
                        cancellationToken: cancellationToken);
                using NatsMemoryOwner<byte> memoryOwner = result.Data;
                (string receivedConnectionId, SerializedHubMessage serializedHubMessage) = memoryOwner.ReadSerializedHubMessageWithConnectionId();
                LoggerMessages.HandleInvokeResult(logger, receivedConnectionId);
                if (TryDeserializeMessage<T>(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
                {
                    LoggerMessages.SuccessfullyDeserializedCompletionMessage(logger, completionMessage.InvocationId!, completionMessage.Result);
                    return completionMessage.HasResult ? (T)completionMessage.Result! : throw new HubException(completionMessage.Error);
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
        if (clientResultsManager.HasInvocation(result.InvocationId!))
        {
            LoggerMessages.SetResultForLocalConnection(logger, connectionId);
            clientResultsManager.TryCompleteResult(connectionId, result);
        }
        else
        {
            LoggerMessages.SetResultForRemoteConnection(logger, connectionId);
            var bufferWriter = new NatsBufferWriter<byte>();
            bufferWriter.WriteCompletionMessageWithConnectionId(result, clientResultsManager.HubProtocols, connectionId);
            await _natsConnection.RequestAsync<NatsBufferWriter<byte>, bool>(NatsSubject.InvokeResultSubject, bufferWriter).ConfigureAwait(false);
        }
    }

    private async Task SendMessageToSubject<T>(string subject, CancellationToken cancellationToken,
        T invocationMessage) where T : HubMessage
    {
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessage(invocationMessage, clientResultsManager.HubProtocols);
        await _natsConnection.PublishAsync(subject, bufferWriter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendGroupMessageToSubject<T>(string subject, string groupName, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken,
        T invocationMessage) where T : HubMessage
    {
        var bufferWriter = new NatsBufferWriter<byte>();
        bufferWriter.WriteMessageWithExcludedConnectionIdsAndGroupName(invocationMessage, clientResultsManager.HubProtocols, groupName, excludedConnectionIds);
        await _natsConnection.PublishAsync(subject, bufferWriter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private bool TryDeserializeMessage(SerializedHubMessage message, [NotNullWhen(true)] out HubMessage? hubMessage)
    {
        foreach (IHubProtocol protocol in protocolResolver.AllProtocols)
        {
            try
            {
                var messagePayload = new ReadOnlySequence<byte>(message.GetSerializedMessage(protocol));
                if (protocol.TryParseMessage(ref messagePayload, clientResultsManager, out hubMessage))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        LoggerMessages.CantDeserializeMessage(logger, protocolResolver.AllProtocols);
        hubMessage = null;
        return false;
    }

    private bool TryDeserializeMessage<T>(SerializedHubMessage message, [NotNullWhen(true)] out HubMessage? hubMessage)
    {
        foreach (IHubProtocol protocol in protocolResolver.AllProtocols)
        {
            try
            {
                var messagePayload = new ReadOnlySequence<byte>(message.GetSerializedMessage(protocol));
                if (protocol.TryParseMessage(ref messagePayload, new RemoteInvocationBinder<T>(), out hubMessage))
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        LoggerMessages.CantDeserializeMessage(logger, protocolResolver.AllProtocols);
        hubMessage = null;
        return false;
    }

    public override bool TryGetReturnType(string invocationId, [NotNullWhen(true)] out Type? type)
    {
        return clientResultsManager.TryGetType(invocationId, out type);
    }
}
