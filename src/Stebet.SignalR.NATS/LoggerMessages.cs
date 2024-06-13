using Microsoft.Extensions.Logging;

namespace Stebet.SignalR.NATS
{
    internal partial class LoggerMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Sending {MethodName} to everyone")]
        internal static partial void Send(ILogger logger, string methodName);

        [LoggerMessage(1, LogLevel.Debug, "Sending {MethodName} to everyone except [{ExcludedConnectionIds}]")]
        internal static partial void SendExcept(ILogger logger, string methodName, IReadOnlyList<string> excludedConnectionIds);

        [LoggerMessage(2, LogLevel.Debug, "Sending {MethodName} to connections [{ConnectionIds}]")]
        internal static partial void SendConnections(ILogger logger, string methodName, IReadOnlyList<string> connectionIds);

        [LoggerMessage(3, LogLevel.Debug, "Sending {MethodName} to groups [{Groups}]")]
        internal static partial void SendGroups(ILogger logger, string methodName, IReadOnlyList<string> groups);

        [LoggerMessage(4, LogLevel.Debug, "Sending {MethodName} to group {Group} but excluding [{ExcludedConnectionIds}]")]
        internal static partial void SendGroupExcept(ILogger logger, string methodName, string group, IReadOnlyList<string> excludedConnectionIds);

        [LoggerMessage(5, LogLevel.Debug, "Sending {MethodName} to users {Users}")]
        internal static partial void SendUsers(ILogger logger, string methodName, IReadOnlyList<string> users);

        [LoggerMessage(6, LogLevel.Debug, "Adding local connection {ConnectionId} to group {Group}")]
        internal static partial void AddLocalConnectionToGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(7, LogLevel.Debug, "Adding remote connection {ConnectionId} to group {Group}")]
        internal static partial void AddRemoteConnectionToGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(8, LogLevel.Debug, "Removing local connection {ConnectionId} from group {Group}")]
        internal static partial void RemoveLocalConnectionFromGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(9, LogLevel.Debug, "Removing remote connection {ConnectionId} from group {Group}")]
        internal static partial void RemoveRemoteConnectionFromGroup(ILogger logger, string connectionId, string group);

        [LoggerMessage(10, LogLevel.Debug, "Invoking {MethodName} on local connection {ConnectionId}")]
        internal static partial void InvokeLocalConnection(ILogger logger, string methodName, string connectionId);

        [LoggerMessage(11, LogLevel.Debug, "Invoking {MethodName} on remote connection {ConnectionId}")]
        internal static partial void InvokeRemoteConnection(ILogger logger, string methodName, string connectionId);

        [LoggerMessage(12, LogLevel.Debug, "Setting result for local connection {ConnectionId}")]
        internal static partial void SetResultForLocalConnection(ILogger logger, string connectionId);

        [LoggerMessage(13, LogLevel.Debug, "Setting result for remote connection {ConnectionId}")]
        internal static partial void SetResultForRemoteConnection(ILogger logger, string connectionId);

        [LoggerMessage(14, LogLevel.Warning, "Can't deserialize message with protocol {ProtocolName}")]
        internal static partial void CantDeserializeMessage(ILogger logger, string protocolName, Exception ex);

        [LoggerMessage(15, LogLevel.Debug, "Handling Invoke Result from {ConnectionId}")]
        internal static partial void HandleInvokeResult(ILogger logger, string connectionId);

        [LoggerMessage(16, LogLevel.Debug, "Successfully deserialized CompletionMessage {InvocationId} with result {Result}")]
        internal static partial void SuccessfullyDeserializedCompletionMessage(ILogger logger, string invocationId, object? result);
    }
}
