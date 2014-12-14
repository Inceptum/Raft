using System;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Follower<TCommand> : NodeState<TCommand>
    {
        public Follower(Node<TCommand> node)
            : base(node,NodeState.Follower)
        {
        }

        public override void Enter()
        {
            Node.Log("I am follower");
            Node.ResetTimeout();
            base.Enter();

        }

        public override void Timeout()
        {
            Node.Log("No Append entries within timeout. ");
            Node.SwitchToCandidate();
        }

        public override bool Handle(VoteRequest voteRequest)
        {
            //Reply false if term < currentTerm
            if (voteRequest.Term < Node.PersistentState.CurrentTerm )
                return false;
            //If votedFor is null or candidateId, and candidate’s log is at least as up-to-date as receiver’s log, grant vote (§5.2, §5.4)
            return 
                (Node.PersistentState.VotedFor == null || 
                Node.PersistentState.VotedFor == voteRequest.CandidateId) &&
                Node.PersistentState.IsLogOlderOrEqual(voteRequest.LastLogIndex, voteRequest.LastLogTerm);
        }

        public override bool Handle(AppendEntriesRequest<TCommand> request)
        {
            Node.Log("Got HB from leader:{0}", Node.LeaderId);

            //Reply false if term < currentTerm (§5.1)
            if (request.Term < Node.PersistentState.CurrentTerm)
            {

                Console.WriteLine("{3} > Rejecting AppendEntries from {0} as term {1} is newer the current {2}",
                    request.LeaderId, request.Term, Node.PersistentState.CurrentTerm, Node.Id);
                return false;
            }

            //Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)
            if(!Node.PersistentState.EntryTermMatches(request.PrevLogIndex,request.PrevLogTerm))
                return false;

            //If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it (§5.3)
            if (Node.PersistentState.DeleteEntriesAfter(request.PrevLogIndex))
            {
                Console.WriteLine("{1} > Deleting conflicting entries startin at {0} ", request.PrevLogIndex+1,Node.Id);
            }
            
            //Append any new entries not already in the log
            if (request.Entries.Any())
                Console.WriteLine("{1} > Appending {0} entries from {2}", request.Entries.Count(), Node.Id,request.LeaderId);
            Node.PersistentState.Append(request.Entries);

            if(request.Entries.Any())
                Console.WriteLine("{1} > Accepting AppendEntries from {0} ", request.LeaderId, Node.Id);

            // If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
            Node.Commit(request.LeaderCommit);

            return true;
        }
 
    }
}