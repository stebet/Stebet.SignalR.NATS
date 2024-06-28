using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Stebet.SignalR.NATS
{
    internal partial class LoggerMessages
    {
        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to everyone")]
        internal static partial void Send(ILogger logger, string methodName);

        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to everyone except [{ExcludedConnectionIds}]")]
        internal static partial void SendExcept(ILogger logger, string methodName, IReadOnlyList<string> excludedConnectionIds);

        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to connections [{ConnectionIds}]")]
        internal static partial void SendConnections(ILogger logger, string methodName, IReadOnlyList<string> connectionIds);

        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to groups [{Groups}]")]
        internal static partial void SendGroups(ILogger logger, string methodName, IReadOnlyList<string> groups);

        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to group {Group} but excluding [{ExcludedConnectionIds}]")]
        internal static partial void SendGroupExcept(ILogger logger, string methodName, string group, IReadOnlyList<string> excludedConnectionIds);

        [LoggerMessage(LogLevel.Debug, "Sending {MethodName} to users {Users}")]
        internal static partial void SendUsers(ILogger logger, string methodName, IReadOnlyList<string> users);

        [LoggerMessage(LogLevel.Debug, "Adding local connection {ConnectionId} to group {Group}")]
        internal static partial void AddLocalConnectionToGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(LogLevel.Debug, "Adding remote connection {ConnectionId} to group {Group}")]
        internal static partial void AddRemoteConnectionToGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(LogLevel.Debug, "Removing local connection {ConnectionId} from group {Group}")]
        internal static partial void RemoveLocalConnectionFromGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(LogLevel.Debug, "Removing remote connection {ConnectionId} from group {Group}")]
        internal static partial void RemoveRemoteConnectionFromGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(LogLevel.Debug, "Invoking {MethodName} on local connection {ConnectionId}")]
        internal static partial void InvokeLocalConnection(ILogger logger, string methodName, string connectionId);

        [LoggerMessage(LogLevel.Debug, "Invoking local connection {ConnectionId}")]
        internal static partial void InvokeLocalConnection(ILogger logger, string connectionId);

        [LoggerMessage(LogLevel.Debug, "Invoking {MethodName} on remote connection {ConnectionId}")]
        internal static partial void InvokeRemoteConnection(ILogger logger, string methodName, string connectionId);

        [LoggerMessage(LogLevel.Debug, "Setting result for local connection {ConnectionId}")]
        internal static partial void SetResultForLocalConnection(ILogger logger, string connectionId);

        [LoggerMessage(LogLevel.Debug, "Setting result for remote connection {ConnectionId}")]
        internal static partial void SetResultForRemoteConnection(ILogger logger, string connectionId);

        [LoggerMessage(LogLevel.Warning, "Can't deserialize message with protocols {Protocols}")]
        internal static partial void CantDeserializeMessage(ILogger logger, IReadOnlyList<IHubProtocol> protocols);

        [LoggerMessage(LogLevel.Debug, "Handling Invoke Result from {ConnectionId}")]
        internal static partial void HandleInvokeResult(ILogger logger, string connectionId);

        [LoggerMessage(LogLevel.Debug, "Successfully deserialized CompletionMessage {InvocationId} with result {Result}")]
        internal static partial void SuccessfullyDeserializedCompletionMessage(ILogger logger, string invocationId, object? result);

        [LoggerMessage(LogLevel.Debug, "Received message {Subject}")]
        internal static partial void ReceivedMessage(ILogger logger, string subject);

        [LoggerMessage(LogLevel.Error, "Error processing message {Subject}")]
        internal static partial void ErrorProcessingMessage(ILogger logger, string subject, Exception e);
        [LoggerMessage(LogLevel.Debug, "Sending message on connection {ConnectionId}")]
        internal static partial void SendConnection(ILogger logger, string connectionId);
        [LoggerMessage(LogLevel.Debug, "Sending message for group {GroupName} on connection {ConnectionId}")]
        internal static partial void SendGroup(ILogger logger, string groupName, string connectionId);
        [LoggerMessage(LogLevel.Debug, "Sending invocation result to {ReplyTo}")]
        internal static partial void SendInvocationResult(ILogger logger, string replyTo);
    }
}
