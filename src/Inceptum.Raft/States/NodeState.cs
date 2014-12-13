using System;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    abstract class NodeState<TCommand> : INodeState<TCommand>
    {
        protected Node<TCommand> Node { get; private set; }
        public NodeState State { get; private set; }
        public DateTime EnterTime { get; private set; }

        protected NodeState(Node<TCommand> node,NodeState state)
        {
            State = state;
            Node = node;
        }


        public virtual void Enter()
        {
            EnterTime = DateTime.Now;
        }
        public abstract void Timeout();
        public abstract bool Handle(VoteRequest voteRequest);

        public virtual void Handle( VoteResponse vote)
        {
            Node.Log("Ignoring RequestVoteResponse since node is not a candidate");
            
        }
        public abstract bool Handle(AppendEntriesRequest<TCommand> request);

        public virtual void Handle( AppendEntriesResponse response)
        {
            Node.Log("Ignoring AppendEntriesResponse since node is not a leader");
        }


        public virtual int GetTimeout(int electionTimeout)
        {
            //random T , 2T
            var buf = Guid.NewGuid().ToByteArray();
            var rnd = BitConverter.ToInt32(buf, 4) % electionTimeout;
            return rnd + electionTimeout;
        }

        public virtual Task<object> Apply(TCommand command)
        {
            throw new NotImplementedException();
        }
    }
}