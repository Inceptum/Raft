using System;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    interface INodeState
    {
        NodeState State { get; }
        DateTime EnterTime { get; }
        void Enter();
        void Timeout();
        bool Handle(VoteRequest voteRequest);
        void Handle(VoteResponse vote);
        bool Handle(AppendEntriesRequest request);
        void Handle(AppendEntriesResponse response);
        int GetTimeout(int electionTimeout);
        Task<object> Apply(object command);
    }
}