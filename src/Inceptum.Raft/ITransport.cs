using System;

namespace Inceptum.Raft
{
    public interface ITransport
    {
        void Send<T>(string to, T message);
        IDisposable Subscribe<T>(Action<T> handler);
    }
}