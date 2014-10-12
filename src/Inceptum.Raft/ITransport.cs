using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    public interface ITransport<TCommand>
    {
        void Send(Guid id, AppendEntriesRequest<TCommand> request, Action<AppendEntriesResponse> callback);
        void Send(Guid id, RequestVoteRequest request, Action<RequestVoteResponse> callback);
        IDisposable Subscribe(Guid id, Func<Guid, AppendEntriesRequest<TCommand>, AppendEntriesResponse> appendEntries);
        IDisposable Subscribe(Guid id, Func<Guid, RequestVoteRequest, RequestVoteResponse> requestVote);
    }
}