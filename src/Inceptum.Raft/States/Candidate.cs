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
            //If election timeout elapses: start new election
            startElection();
        }

        private void startElection()
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

            Node.ResetTimeout();
            Node.RequestVotes();
        }

        public override void Timeout()
        {
            //On conversion to candidate, start election
            startElection();
        }

        public override bool RequestVote(RequestVoteRequest request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }

        public override void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            m_Votes.Add(node,vote);
            if(m_Votes.Values.Count(v=>v.VoteGranted)>=Node.Configuration.Majority)
                Node.SwitchToLeader();
        }

        public override bool AppendEntries(AppendEntriesRequest request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }
 
    }
}