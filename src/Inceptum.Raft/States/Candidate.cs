using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Candidate : NodeStateImpl
    {
        private Dictionary<string, VoteResponse> m_Votes;

        public Candidate(Node node)
            : base(node,NodeState.Candidate)
        {
        }

        public override void Enter()
        {
            //On conversion to candidate, start election
            startElection();
            base.Enter();
        }

        private void startElection()
        {
            Node.ResetTimeout();
            Node.Logger.Trace("Starting Election");
            m_Votes = new Dictionary<string, VoteResponse>();
            //vote for itself
            Handle(new VoteResponse
            {
                NodeId = Node.Id,
                Term = Node.IncrementTerm(),
                VoteGranted = true
            });

            Node.RequestVotes();
        }

        public override void Timeout()
        {
            //If election timeout elapses: start new election
            startElection();
        }

        public override bool Handle(VoteRequest voteRequest)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }

        public override void Handle(VoteResponse vote)
        {
            if (vote.Term != Node.CurrentTerm)
            {
                //Ignore respnses from older terms
                return;
            }
            //It is possible to get multiple VoteReponses from same node. It happends if after term incremented it gets 
            //responce for request, sent in previous terms. 
            //If voter had same term as curent node has right now such response will reach this point. 
            //It is ok since node grants vote to single node during term. 
            //Spent lot of time to find out why the assert is firing :)
            //Debug.Assert(!m_Votes.ContainsKey(vote.NodeId));

            m_Votes[vote.NodeId] = vote;
            if (m_Votes.Values.Count(v => v.VoteGranted) >= Node.Majority)
            {
                var grantedBy = string.Join(", ",m_Votes.Values.Where(v => v.VoteGranted).Select(r => r.NodeId).ToArray());
                Node.Logger.Debug("Vote granted by majority of nodes - {0}. Switching state to Leader", grantedBy);
                Node.SwitchToLeader();
            }

        }

        public override bool Handle(AppendEntriesRequest request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }
 
    }
}