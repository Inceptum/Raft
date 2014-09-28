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
        public abstract bool RequestVote(RequestVoteRequest request);

        public virtual void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            Node.Log("Ignoring RequestVoteResponse since node is not a candidate");
            
        }
        public abstract bool AppendEntries(AppendEntriesRequest request);

        public virtual void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse response)
        {
            Node.Log("Ignoring AppendEntriesResponse since node is not a leader");
        }
    }
}