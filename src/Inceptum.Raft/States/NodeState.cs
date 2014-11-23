using System;
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
        public abstract bool RequestVote(RequestVoteRequest request);

        public virtual void ProcessVote( RequestVoteResponse vote)
        {
            Node.Log("Ignoring RequestVoteResponse since node is not a candidate");
            
        }
        public abstract bool AppendEntries(AppendEntriesRequest<TCommand> request);

        public virtual void ProcessAppendEntriesResponse( AppendEntriesResponse response)
        {
            Node.Log("Ignoring AppendEntriesResponse since node is not a leader");
        }


        public virtual int GetTimeout(int electionTimeout)
        {

            //random T , 2T
            var rndNum = new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8), System.Globalization.NumberStyles.HexNumber));
            int rnd = rndNum.Next(0, electionTimeout);
          return rnd + electionTimeout;

        }
    }
}