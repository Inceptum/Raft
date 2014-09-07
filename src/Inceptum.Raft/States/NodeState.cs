using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    abstract class NodeState : INodeState
    {
        protected Node Node { get; private set; }

        protected NodeState(Node node)
        {
            Node = node;
        }

        public abstract void Enter();
        public abstract void Timeout();
        public abstract RequestVoteResponse RequestVote(RequestVoteRequest request);
        public abstract void ProcessVote(Guid node, RequestVoteResponse vote);
        public abstract AppendEntriesResponse AppendEntries(AppendEntriesRequest request);
        public abstract void ProcessAppendEntries(Guid node, AppendEntriesResponse appendEntriesResponse);
    }
}