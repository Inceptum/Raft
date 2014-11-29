using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Candidate<TCommand> : NodeState<TCommand>
    {
        private Dictionary<Guid, RequestVoteResponse> m_Votes;

        public Candidate(Node<TCommand> node)
            : base(node,NodeState.Candidate)
        {
        }

        public override void Enter()
        {
            //On conversion to candidate, start election
            startElection();
            Node.Log("I am candidate");
            base.Enter();
        }

        private void startElection()
        {
            Node.ResetTimeout();
            Node.Log("Starting Election");
            m_Votes = new Dictionary<Guid, RequestVoteResponse>();
            //vote for itself
            Handle(new RequestVoteResponse
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

        public override bool Handle(RequestVoteRequest request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }

        public override void Handle(RequestVoteResponse vote)
        {
            Debug.Assert(!m_Votes.ContainsKey(vote.NodeId));

            m_Votes[vote.NodeId] = vote;
            if (m_Votes.Values.Count(v => v.VoteGranted) >= Node.Configuration.Majority)
                Node.SwitchToLeader();

        }

        public override bool Handle(AppendEntriesRequest<TCommand> request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }
 
    }
}