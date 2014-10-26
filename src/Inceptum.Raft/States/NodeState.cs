using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    abstract class NodeState<TCommand> : INodeState<TCommand>
    {
        protected Node<TCommand> Node { get; private set; }
        public NodeState State { get; private set; }

        protected NodeState(Node<TCommand> node,NodeState state)
        {
            State = state;
            Node = node;
        }


        public abstract void Enter();
        public abstract void Timeout();
        public abstract bool RequestVote(RequestVoteRequest request);

        public virtual void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            Node.Log("Ignoring RequestVoteResponse since node is not a candidate");
            
        }
        public abstract bool AppendEntries(AppendEntriesRequest<TCommand> request);

        public virtual void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse response)
        {
            Node.Log("Ignoring AppendEntriesResponse since node is not a leader");
        }
    }
}