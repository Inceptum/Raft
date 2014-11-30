using System;

namespace Inceptum.Raft
{
    public interface ITransport
    {
        void Send<T>(string from, string to, T message);
        IDisposable Subscribe<T>(string subscriberId, Action<T> handler);
    }
}