using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    interface INodeState<TCommand>
    {
        NodeState State { get; }
        DateTime EnterTime { get; }
        void Enter();
        void Timeout();
        bool RequestVote(RequestVoteRequest request);
        void ProcessVote(Guid node,RequestVoteResponse vote);
        bool AppendEntries(AppendEntriesRequest<TCommand> request);
        void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse response);
    }
}