using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace Stebet.SignalR.NATS
{
    internal partial class NatsHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private async Task StartSubscriptions()
        {
            INatsSub<NatsMemoryOwner<byte>> sub = await _natsConnection.SubscribeCoreAsync<NatsMemoryOwner<byte>>($"{NatsSubject.GlobalSubjectPrefix}.>").ConfigureAwait(false);
            await _natsConnection.PingAsync().ConfigureAwait(false);
            await Parallel.ForEachAsync(sub.Msgs.ReadAllAsync(), Helpers.DefaultParallelOptions,
                async (message, token) =>
                {
                    using NatsMemoryOwner<byte> messageDataOwner = message.Data;
                    try
                    {
                        LoggerMessages.ReceivedMessage(logger, message.Subject);
                        if (message.Subject == NatsSubject.ConnectionDisconnectedSubject)
                        {
                            HandleConnectionDisconnected(messageDataOwner);
                        }
                        else if (message.Subject == NatsSubject.InvokeResultSubject)
                        {
                            HandleInvokeResult(messageDataOwner);
                            await message.ReplyAsync(true, cancellationToken: token).ConfigureAwait(false);
                        }
                        else if (message.Subject == NatsSubject.AllConnectionsSendSubject)
                        {
                            var tasks = new List<Task>();
                            (SortedSet<string> excludedConnections, SerializedHubMessage serializedHubMessage) = messageDataOwner.ReadSerializedHubMessageWithExcludedConnectionIds();
                            foreach (var connection in _connections)
                            {
                                if (excludedConnections.Count > 0 && excludedConnections.Contains(connection.ConnectionId))
                                {
                                    continue;
                                }

                                tasks.Add(connection.WriteAsync(serializedHubMessage, CancellationToken.None).AsTask());
                            }

                            await Task.WhenAll(tasks).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        LoggerMessages.ErrorProcessingMessage(logger, message.Subject, e);
                    }
                }).ConfigureAwait(false);
        }

        private void HandleInvokeResult(NatsMemoryOwner<byte> memory)
        {
            (string connectionId, SerializedHubMessage serializedHubMessage) = memory.ReadSerializedHubMessageWithConnectionId();
            LoggerMessages.HandleInvokeResult(logger, connectionId);
            if (TryDeserializeMessage(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
            {
                LoggerMessages.SuccessfullyDeserializedCompletionMessage(logger, completionMessage.InvocationId!, completionMessage.Result);
                clientResultsManager.TryCompleteResult(connectionId, completionMessage);
            }
        }

        private void HandleConnectionDisconnected(NatsMemoryOwner<byte> memory)
        {
            string connectionId = memory.ReadString();
            if (_connections[connectionId] is null)
            {
                clientResultsManager.AbortInvocationsForConnection(connectionId);
            }
        }
    }
}
