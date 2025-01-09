namespace Stebet.SignalR.NATS
{
    internal struct RemoteInvocationBinder<T> : IInvocationBinder
    {
        public readonly IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();
        public readonly Type GetReturnType(string invocationId) => typeof(T);
        public readonly Type GetStreamItemType(string streamId) => throw new NotImplementedException();
    }
}
