using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Leader : NodeState
    {

        /// <summary>
        /// For each server, index of the next log entry to send to that server (initialized to leader last log index + 1).
        /// </summary>
        /// <value>
        /// The next indexes.
        /// </value>
        public Dictionary<Guid,int> NextIndexes { get; private set; }
        /// <summary>
        /// For each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the match.
        /// </value>
        public Dictionary<Guid, int> MatchIndex { get; private set; }

        public Dictionary<Guid, int> LastSentIndex { get; private set; }

        public Leader(Node node)
            : base(node)
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
            }
            Node.ResetTimeout(.3);
            Timeout();
        }

        public override void Timeout()
        {
            Node.Log("I AM LEADER!!!!! Sending HB");
            //Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)
            var request = new AppendEntriesRequest
            {
                Term = Node.PersistentState.CurrentTerm,
                Entries = null,
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

        public override bool AppendEntries(AppendEntriesRequest request)
        {
            //Since state is not sitched to follower, requester term is older or same - decline
            return false;
        }

        public override void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse response)
        {
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
                var request = new AppendEntriesRequest
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