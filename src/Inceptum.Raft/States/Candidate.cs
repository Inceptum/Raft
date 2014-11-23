using System;
using System.Collections.Generic;
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
            //TODO: Crappy code, two places where votedFor is set...
            Node.PersistentState.VotedFor = Node.Id;

            Node.RequestVotes();
            Node.ResetTimeout();
        }

        public override void Timeout()
        {
            //If election timeout elapses: start new election
            startElection();
        }

        public override bool RequestVote(RequestVoteRequest request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }

        public override void ProcessVote(RequestVoteResponse vote)
        {
            if (m_Votes.ContainsKey(vote.NodeId))
                Console.WriteLine("!!!");
            m_Votes[vote.NodeId] = vote;
            if (m_Votes.Values.Count(v => v.VoteGranted) >= Node.Configuration.Majority)
                Node.SwitchToLeader();

        }

        public override bool AppendEntries(AppendEntriesRequest<TCommand> request)
        {
            //term in request is not newer than our (otherwise state should have been already changed to follower)
            return false;
        }
 
    }
}