namespace Stebet.SignalR.NATS
{
    internal struct RemoteInvocationBinder<T> : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();
        public Type GetReturnType(string invocationId) => typeof(T);
        public Type GetStreamItemType(string streamId) => throw new NotImplementedException();
    }
}
