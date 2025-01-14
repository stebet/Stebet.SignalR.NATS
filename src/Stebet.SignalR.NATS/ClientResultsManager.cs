using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Stebet.SignalR.NATS;

// Common type used by our HubLifetimeManager implementations to manage client results.
// Handles cancellation, cleanup, and completion, so any bugs or improvements can be made in a single place
internal sealed class ClientResultsManager<THub> : IInvocationBinder where THub : Hub
{
    private readonly ConcurrentDictionary<string, (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)> _pendingInvocations = new();

    internal IReadOnlyList<IHubProtocol> HubProtocols { get; }

    public ClientResultsManager(IHubProtocolResolver hubProtocolResolver, IOptions<HubOptions> globalHubOptions, IOptions<HubOptions<THub>> hubOptions)
    {
        IList<string> supportedProtocols = hubOptions.Value.SupportedProtocols ?? globalHubOptions.Value.SupportedProtocols ?? Array.Empty<string>();
        var hubProtocols = new List<IHubProtocol>(supportedProtocols.Count);
        foreach (string protocolName in supportedProtocols)
        {
            IHubProtocol? protocol = hubProtocolResolver.GetProtocol(protocolName, (supportedProtocols as IReadOnlyList<string>) ?? [.. supportedProtocols]);
            if (protocol != null)
            {
                hubProtocols.Add(protocol);
            }
        }

        HubProtocols = hubProtocols.AsReadOnly();
    }

    public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSourceWithCancellation<T>(this, connectionId, invocationId, cancellationToken);
        bool result = _pendingInvocations.TryAdd(invocationId, (typeof(T), connectionId, tcs, static (state, completionMessage) =>
        {
            var tcs = (TaskCompletionSourceWithCancellation<T>)state;
            if (completionMessage.HasResult)
            {
                tcs.SetResult((T)completionMessage.Result!);
            }
            else
            {
                tcs.SetException(new HubException(completionMessage.Error));
            }
        }
        ));
        Debug.Assert(result);

        tcs.RegisterCancellation();

        return tcs.Task;
    }

    public void AddInvocation(string invocationId, (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) invocationInfo)
    {
        bool result = _pendingInvocations.TryAdd(invocationId, invocationInfo);
        Debug.Assert(result);
        // Should have a 50% chance of happening once every 2.71 quintillion invocations (see UUID in Wikipedia)
        if (!result)
        {
            invocationInfo.Complete(invocationInfo.Tcs, CompletionMessage.WithError(invocationId, "ID collision occurred when using client results. This is likely a bug in SignalR."));
        }
    }

    public void AbortInvocationsForConnection(string connectionId)
    {
        foreach (KeyValuePair<string, (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)> item in _pendingInvocations)
        {
            if (item.Value.ConnectionId == connectionId && _pendingInvocations.Remove(item.Key, out (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) _))
            {
                item.Value.Complete(item.Value.Tcs, CompletionMessage.WithError(item.Key, "Connection disconnected."));
            }
        }
    }

    public void TryCompleteResult(string connectionId, CompletionMessage message)
    {
        if (_pendingInvocations.TryGetValue(message.InvocationId!, out (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) item))
        {
            if (item.ConnectionId != connectionId)
            {
                throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
            }

            // if false the connection disconnected right after the above TryGetValue
            // or someone else completed the invocation (likely a bad client)
            // we'll ignore both cases
            if (_pendingInvocations.Remove(message.InvocationId!, out _))
            {
                item.Complete(item.Tcs, message);
            }
        }
        else
        {
            // connection was disconnected or someone else completed the invocation
        }
    }

    public bool HasInvocation(string invocationId) => _pendingInvocations.ContainsKey(invocationId);

    public (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Completion)? RemoveInvocation(string invocationId)
    {
        _pendingInvocations.TryRemove(invocationId, out (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) item);
        return item;
    }

    public bool TryGetType(string invocationId, [NotNullWhen(true)] out Type? type)
    {
        if (_pendingInvocations.TryGetValue(invocationId, out (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) item))
        {
            type = item.Type;
            return true;
        }
        type = null;
        return false;
    }

    public Type GetReturnType(string invocationId)
    {
        if (TryGetType(invocationId, out Type? type))
        {
            return type;
        }

        throw new InvalidOperationException($"Invocation ID '{invocationId}' is not associated with a pending client result.");
    }

    // Unused, here to honor the IInvocationBinder interface but should never be called
    public IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();

    // Unused, here to honor the IInvocationBinder interface but should never be called
    public Type GetStreamItemType(string streamId) => throw new NotImplementedException();

    // Custom TCS type to avoid the extra allocation that would be introduced if we managed the cancellation separately
    // Also makes it easier to keep track of the CancellationTokenRegistration for disposal
    internal sealed class TaskCompletionSourceWithCancellation<T>(ClientResultsManager<THub> clientResultsManager, string connectionId, string invocationId,
        CancellationToken cancellationToken) : TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        private CancellationTokenRegistration _tokenRegistration;

        // Needs to be called after adding the completion to the dictionary in order to avoid synchronous completions of the token registration
        // not canceling when the dictionary hasn't been updated yet.
        public void RegisterCancellation()
        {
            if (cancellationToken.CanBeCanceled)
            {
                _tokenRegistration = cancellationToken.UnsafeRegister(static o =>
                {
                    var tcs = (TaskCompletionSourceWithCancellation<T>)o!;
                    tcs.SetCanceled();
                }, this);
            }
        }

        public new void SetCanceled() =>
            // TODO: RedisHubLifetimeManager will want to notify the other server (if there is one) about the cancellation
            // so it can clean up state and potentially forward that info to the connection
            clientResultsManager.TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Invocation canceled by the server."));

        public new void SetResult(T result)
        {
            _tokenRegistration.Dispose();
            base.SetResult(result);
        }

        public new void SetException(Exception exception)
        {
            _tokenRegistration.Dispose();
            base.SetException(exception);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        // Just making sure we don't accidentally call one of these without knowing
        public static new void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);
        public static new void SetException(IEnumerable<Exception> exceptions) => Debug.Assert(false);
        public static new bool TrySetCanceled()
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetCanceled(CancellationToken cancellationToken)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetException(IEnumerable<Exception> exceptions)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetException(Exception exception)
        {
            Debug.Assert(false);
            return false;
        }
        public static new bool TrySetResult(T result)
        {
            Debug.Assert(false);
            return false;
        }
#pragma warning restore IDE0060 // Remove unused parameter
    }
}
