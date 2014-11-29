using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Leader<TCommand> : NodeState<TCommand>
    {

        /// <summary>
        /// For each server, index of the next log entry to send to that server (initialized to leader last log index + 1).
        /// </summary>
        /// <value>
        /// The next indexes.
        /// </value>
        private Dictionary<Guid,int> NextIndexes { get; set; }
        /// <summary>
        /// For each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the match.
        /// </value>
        private Dictionary<Guid, int> MatchIndex { get; set; }

        private Dictionary<Guid, int> LastSentIndex { get; set; }

        public Leader(Node<TCommand> node)
            : base(node,NodeState.Leader)
        {
            NextIndexes = new Dictionary<Guid, int>();
            MatchIndex = new Dictionary<Guid, int>();
            LastSentIndex = new Dictionary<Guid, int>();
        }

        public override void Enter()
        {
            Node.Log("I am leader");

            foreach (var node in Node.Configuration.KnownNodes)
            {
                NextIndexes[node] = Node.PersistentState.Log.Count;
                MatchIndex[node] = 0;
                LastSentIndex[node] = 0;
            }
            Node.ResetTimeout();
            Timeout();
            base.Enter();
        }

        public override int GetTimeout(int electionTimeout)
        {
            return (int)Math.Round(1.0*electionTimeout /3);
        }

        public override void Timeout()
        {
            Node.Log("I AM THE LEADER!!!!! Sending HB");
            //Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)

            var request = new AppendEntriesRequest<TCommand>
            {
                Term = Node.PersistentState.CurrentTerm,
                Entries = new ILogEntry<TCommand>[0],
                LeaderCommit = Node.CommitIndex,
                LeaderId = Node.Id,
                PrevLogIndex = Node.PersistentState.Log.Count - 1,
                PrevLogTerm = Node.PersistentState.Log.Select(e => e.Term).LastOrDefault()
            };

            foreach (var node in Node.Configuration.KnownNodes)
            {
                Node.AppendEntries(node, request);
            }
        }
        //TODO: If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)

        public override bool RequestVote(RequestVoteRequest request)
        {
            //Since state is not sitched to follower, term is older or same - decline
            return false;
        }

        public override bool AppendEntries(AppendEntriesRequest<TCommand> request)
        {
            //Since state is not sitched to follower, requester term is older or same - decline
            return false;
        }

        public override void ProcessAppendEntriesResponse( AppendEntriesResponse response)
        {
            var node = response.NodeId;
            //TODO: If there exists an N such that N > commitIndex, a majority of matchIndex[i] ≥ N, and log[N].term == currentTerm: set commitIndex = N (§5.3, §5.4)
            if (response.Success)
            {
                MatchIndex[node] = LastSentIndex[node];
                NextIndexes[node] = LastSentIndex[node] + 1;
            }
            else
            {
                var nextIndex=--NextIndexes[node];
                var lastSentIndex = Node.PersistentState.Log.Count;
                var request = new AppendEntriesRequest<TCommand>
                {
                    Term = Node.PersistentState.CurrentTerm,
                    Entries = Node.PersistentState.Log.GetRange(nextIndex, lastSentIndex-nextIndex),
                    LeaderCommit = Node.CommitIndex,
                    LeaderId = Node.Id,
                    PrevLogIndex = lastSentIndex - 1,
                    PrevLogTerm = Node.PersistentState.Log.Select(e => e.Term).LastOrDefault()
                };
                LastSentIndex[node] = lastSentIndex;
                Node.AppendEntries(node, request);
            }

        }
    }
}