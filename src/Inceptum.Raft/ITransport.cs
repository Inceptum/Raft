using System;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    public interface ITransport
    {
        void Send(string to, AppendEntriesRequest message);
        void Send(string to, AppendEntriesResponse message);
        void Send(string to, VoteRequest message);
        void Send(string to, VoteResponse message);
        Task<object> Send(string to, ApplyCommadRequest message);
        IDisposable Subscribe<T>(Func<T, Task<object>> handler);
    }
}