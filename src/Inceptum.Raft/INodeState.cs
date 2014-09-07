using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    interface INodeState
    {
        void Enter();
        void Timeout();
        RequestVoteResponse RequestVote(RequestVoteRequest request);
        void ProcessVote(Guid node,RequestVoteResponse vote);
        AppendEntriesResponse AppendEntries(AppendEntriesRequest request);
        void ProcessAppendEntries(Guid node, AppendEntriesResponse appendEntriesResponse);
    }
}