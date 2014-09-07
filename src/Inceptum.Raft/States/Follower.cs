using System;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Follower : NodeState
    {

        public Follower(Node node)
            : base(node)
        {
        }


        public override void Enter()
        {
            throw new NotImplementedException();
        }

        public override void Timeout()
        {
            Node.SwitchToCandidate();
        }

        public override RequestVoteResponse RequestVote(RequestVoteRequest request)
        {
            throw new NotImplementedException();
            Node.ResetTimeout();
        }

        public override void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            throw new NotImplementedException();
        }

        public override AppendEntriesResponse AppendEntries(AppendEntriesRequest request)
        {
            throw new NotImplementedException();
            Node.ResetTimeout();
        }

        public override void ProcessAppendEntries(Guid node, AppendEntriesResponse appendEntriesResponse)
        {
            throw new NotImplementedException();
        }
    }
}