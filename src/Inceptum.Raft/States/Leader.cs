using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.States
{
    class Leader : NodeStateImpl
    {

        /// <summary>
        /// For each server, index of the next log entry to send to that server (initialized to leader last log index + 1).
        /// </summary>
        /// <value>
        /// The next indexes.
        /// </value>
        private Dictionary<string, int> NextIndexes { get; set; }
        /// <summary>
        /// For each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the match.
        /// </value>
        private Dictionary<string, int> MatchIndex { get; set; }

        private Dictionary<string, int> LastSentIndex { get; set; }

        public Leader(Node node)
            : base(node,NodeState.Leader)
        {
            NextIndexes = new Dictionary<string, int>();
            MatchIndex = new Dictionary<string, int>();
            LastSentIndex = new Dictionary<string, int>();
        }

        public override void Enter()
        {

            foreach (var node in Node.KnownNodes)
            {
                NextIndexes[node] = Node.PersistentState.Log.Count;
                MatchIndex[node] = -1;
                LastSentIndex[node] = -1;
            }
            Node.ResetTimeout();
            //Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)
            appendEntries();
            base.Enter();
        }

        public override int GetTimeout(int electionTimeout)
        {
            return (int)Math.Round(1.0*electionTimeout /3);
        }

        public override void Timeout()
        {
            Node.Logger.Trace("Sending HB");
            //Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)
            appendEntries();
        }

        public override Task<object> Apply(object command)
        {
            var completion = Node.PersistentState.Append(command);
            appendEntries();
            Node.ResetTimeout();

            //If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)
            return completion.Task;
        }

        private void appendEntries()
        {
            foreach (var node in Node.KnownNodes)
            {
                var nextIndex = NextIndexes[node];

                IEnumerable<LogEntry> logEntries = new LogEntry[0];
                if (nextIndex < Node.PersistentState.Log.Count)
                {
                    var entriesCount = Math.Min(20, Node.PersistentState.Log.Count - nextIndex); //TODO: move batch size to config (20)
                    logEntries = Node.PersistentState.GetRange(nextIndex, entriesCount);
                    LastSentIndex[node] = nextIndex + entriesCount - 1;
                    Console.WriteLine("{2} > Sending {0} entries to {1}",entriesCount,node,Node.Id);
                }

                var request = new AppendEntriesRequest
                {
                    Term = Node.PersistentState.CurrentTerm,
                    Entries = logEntries,
                    LeaderCommit = Node.CommitIndex,
                    LeaderId = Node.Id,
                    PrevLogIndex = nextIndex > 0 ? nextIndex - 1 : -1,
                    PrevLogTerm = nextIndex > 0 ? Node.PersistentState.Log[nextIndex - 1].Term : -1
                };
                Node.AppendEntries(node, request);
            }
        }

        public override bool Handle(VoteRequest voteRequest)
        {
            //Since state is not sitched to follower, term is older or same - decline
            return false;
        }

        public override bool Handle(AppendEntriesRequest request)
        {
            //Since state is not sitched to follower, requester term is older or same - decline
            return false;
        }

        public override void Handle( AppendEntriesResponse response)
        {
            var node = response.NodeId;
            if (response.Success)
            {
                MatchIndex[node] = LastSentIndex[node];
                NextIndexes[node] = LastSentIndex[node] + 1;
                if (!Node.PersistentState.Log.Any()) 
                    return;

                //If there exists an N such that N > commitIndex, a majority of matchIndex[i] ≥ N, and log[N].term == currentTerm: set commitIndex = N (§5.3, §5.4)
                for (var i = Node.CommitIndex+1; i <= MatchIndex.Values.Max(); i++)
                {
                    if (MatchIndex.Values.Count(mi => mi >= Node.CommitIndex) >= Node.Majority &&
                        Node.PersistentState.Log[i].Term == Node.CurrentTerm)
                    {
                        Console.WriteLine(Node.Id + "|" + Node.CurrentTerm + " > APPLY(leader): " + i);
                        Node.Commit(i);
                    }
                }
            }
            else
            {
                if (NextIndexes[node]>0)
                    NextIndexes[node]--;
            }

        }
    }
}