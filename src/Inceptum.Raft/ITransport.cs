using System;

namespace Inceptum.Raft
{
    public interface ITransport
    {
        void Send<T>(Guid from, Guid to, T message);
        IDisposable Subscribe<T>(Guid subscriberId, Action<T> handler);
    }
}