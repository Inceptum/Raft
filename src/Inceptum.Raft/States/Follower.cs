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

        public override bool Handle(RequestVoteRequest request)
        {
            //Reply false if term < currentTerm
            if (request.Term < Node.PersistentState.CurrentTerm )
                return false;
            //If votedFor is null or candidateId, and candidate’s log is at least as up-to-date as receiver’s log, grant vote (§5.2, §5.4)
            return 
                (Node.PersistentState.VotedFor == default(Guid) || 
                Node.PersistentState.VotedFor == request.CandidateId) &&
                Node.PersistentState.IsLogOlderOrEqual(request.LastLogIndex, request.LastLogTerm);
        }

        public override bool Handle(AppendEntriesRequest<TCommand> request)
        {
           
            //Reply false if term < currentTerm (§5.1)
            if (request.Term < Node.PersistentState.CurrentTerm)
                return false;

            //Reply false if log doesn’t contain an entry at prevLogIndex whose term matches prevLogTerm (§5.3)
            if(!Node.PersistentState.EntryTermMatches(request.PrevLogIndex,request.PrevLogTerm))
                return false;

            //If an existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it (§5.3)
            Node.PersistentState.DeleteEntriesAfter(request.PrevLogIndex);
            
            //Append any new entries not already in the log
            Node.PersistentState.Append(request.Entries);

            // If leaderCommit > commitIndex, set commitIndex = min(leaderCommit, index of last new entry)
            Node.Commit(request.LeaderCommit);

            return true;
        }
 
    }
}