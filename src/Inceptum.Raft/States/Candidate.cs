using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Candidate : NodeState
    {
        private Dictionary<Guid, RequestVoteResponse> m_Votes;

        public Candidate(Node node):base(node)
        {
        }

        public override void Enter()
        { 
            m_Votes = new Dictionary<Guid, RequestVoteResponse>
            {
                {
                    Node.Id, 
                    new RequestVoteResponse
                        {
                            Term = Node.IncrementTerm(), 
                            VoteGranted = true
                        }
                }
            };

            Node.RequestVotes();
            Node.ResetTimeout();
        }

        public override void Timeout()
        {
            throw new System.NotImplementedException();
        }

        public override RequestVoteResponse RequestVote(RequestVoteRequest request)
        {
            throw new NotImplementedException();
        }

        public override void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            m_Votes.Add(node,vote);
            if(m_Votes.Values.Count(v=>v.VoteGranted)>=Node.Configuration.Majority)
                Node.SwitchToLeader();
        }

        public override AppendEntriesResponse AppendEntries(AppendEntriesRequest request)
        {

            throw new NotImplementedException();
        }

        public override void ProcessAppendEntries(Guid node, AppendEntriesResponse appendEntriesResponse)
        {
            throw new NotImplementedException();
        }
    }
}