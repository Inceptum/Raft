using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    interface INodeState
    {
        void Enter();
        void Timeout();
        bool RequestVote(RequestVoteRequest request);
        void ProcessVote(Guid node,RequestVoteResponse vote);
        bool AppendEntries(AppendEntriesRequest request);
        void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse appendEntriesResponse);
    }
}