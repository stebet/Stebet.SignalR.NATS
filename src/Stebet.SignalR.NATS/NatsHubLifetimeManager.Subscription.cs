using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

using NATS.Client.Core;

namespace Stebet.SignalR.NATS
{
    internal partial class NatsHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private async Task StartSubscriptions()
        {
            INatsSub<NatsMemoryOwner<byte>> sub = await natsConnection.SubscribeCoreAsync<NatsMemoryOwner<byte>>($"{NatsSubject.GlobalSubjectPrefix}.>").ConfigureAwait(false);
            await natsConnection.PingAsync().ConfigureAwait(false);
            await Parallel.ForEachAsync(sub.Msgs.ReadAllAsync(), Helpers.DefaultParallelOptions,
                async (message, token) =>
                {
                    using NatsMemoryOwner<byte> messageDataOwner = message.Data;
                    try
                    {
                        logger.LogDebug("Received message {Subject}", message.Subject);
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
                        logger.LogError(e, "Error processing message {Subject}", message.Subject);
                    }
                }).ConfigureAwait(false);
        }

        private void HandleInvokeResult(NatsMemoryOwner<byte> memory)
        {
            (string connectionId, SerializedHubMessage serializedHubMessage) = memory.ReadSerializedHubMessageWithConnectionId();
            logger.LogDebug("Handling Invoke Result from {ConnectionId}", connectionId);
            if (TryDeserializeMessage(serializedHubMessage, out HubMessage? hubMessage) && hubMessage is CompletionMessage completionMessage)
            {
                logger.LogDebug("Successfully deserialized CompletionMessage {InvocationId} with result {Result}", completionMessage.InvocationId, completionMessage.Result);

                clientResultsManager.TryCompleteResult(connectionId, completionMessage);
            }
            else
            {
                logger.LogWarning("Failed to deserialize completion message");
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
