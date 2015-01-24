using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    public interface ITransport
    {
        void Send<T>(string to, AppendEntriesRequest<T> message);
        void Send(string to, AppendEntriesResponse message);
        void Send(string to, VoteRequest message);
        void Send(string to, VoteResponse message);
        IDisposable Subscribe<T>(Action<T> handler);
    }
}