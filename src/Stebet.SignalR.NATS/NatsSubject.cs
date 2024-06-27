namespace Stebet.SignalR.NATS;

internal static class NatsSubject
{
    internal static string Prefix { get; set; } = "signalr.nats";
    internal static string GlobalSubjectPrefix => $"{Prefix}.send";
    private static string GetConnectionPrefix(string connectionId) => $"{Prefix}.connection.{connectionId}";
    internal static string GetConnectionInvokeSubject(string connectionId) => $"{GetConnectionPrefix(connectionId)}.invoke";
    internal static string GetConnectionSendSubject(string connectionId) => $"{GetConnectionPrefix(connectionId)}.send";
    internal static string GetConnectionGroupWildcardSubject(string connectionId) => $"{GetConnectionPrefix(connectionId)}.group.*";
    internal static string GetConnectionGroupAddSubject(string connectionId) => $"{GetConnectionPrefix(connectionId)}.group.add";
    internal static string GetConnectionGroupRemoveSubject(string connectionId) => $"{GetConnectionPrefix(connectionId)}.group.remove";
    internal static string GetUserSendSubject(string userId) => $"{Prefix}.user.{userId}.send";
    internal static string GetGroupSendSubject(string groupName) => $"{Prefix}.group.{groupName}.send";
    internal static string InvokeResultSubject => $"{GlobalSubjectPrefix}.invoke.result";
    internal static string ConnectionDisconnectedSubject => $"{GlobalSubjectPrefix}.connection.disconnected";
    internal static string AllConnectionsSendSubject => $"{GlobalSubjectPrefix}.allconnections";
}
