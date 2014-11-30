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
        bool Handle(RequestVoteRequest request);
        void Handle(RequestVoteResponse vote);
        bool Handle(AppendEntriesRequest<TCommand> request);
        void Handle(AppendEntriesResponse response);
        int GetTimeout(int electionTimeout);
        void Apply(TCommand command);
    }
}