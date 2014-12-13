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
        private Dictionary<string, int> NextIndexes { get; set; }
        /// <summary>
        /// For each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the match.
        /// </value>
        private Dictionary<string, int> MatchIndex { get; set; }

        private Dictionary<string, int> LastSentIndex { get; set; }

        public Leader(Node<TCommand> node)
            : base(node,NodeState.Leader)
        {
            NextIndexes = new Dictionary<string, int>();
            MatchIndex = new Dictionary<string, int>();
            LastSentIndex = new Dictionary<string, int>();
        }

        public override void Enter()
        {
            Node.Log("I am leader");

            foreach (var node in Node.Configuration.KnownNodes)
            {
                NextIndexes[node] = Node.PersistentState.Log.Count;
                MatchIndex[node] = -1;
                LastSentIndex[node] = -1;
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
            appendEntries();
        }

        public override void Apply(TCommand command)
        {
            Node.PersistentState.Append(command);
            //TODO: proxy to leader
            appendEntries();
            Node.ResetTimeout();
        }

        private void appendEntries()
        {
            foreach (var node in Node.Configuration.KnownNodes)
            {
                var nextIndex = NextIndexes[node];

                IEnumerable<ILogEntry<TCommand>> logEntries = new ILogEntry<TCommand>[0];
                if (nextIndex < Node.PersistentState.Log.Count)
                {
                    var entriesCount = Math.Min(20, Node.PersistentState.Log.Count - nextIndex); //TODO: move batch size to config (20)
                    logEntries = Node.PersistentState.Log.GetRange(nextIndex, entriesCount);
                    LastSentIndex[node] = nextIndex + entriesCount - 1;
                    Console.WriteLine("{2} > Sending {0} entries to {1}",entriesCount,node,Node.Id);
                }

                var request = new AppendEntriesRequest<TCommand>
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

        //TODO: If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)

        public override bool Handle(VoteRequest voteRequest)
        {
            //Since state is not sitched to follower, term is older or same - decline
            return false;
        }

        public override bool Handle(AppendEntriesRequest<TCommand> request)
        {
            //Since state is not sitched to follower, requester term is older or same - decline
            return false;
        }

        public override void Handle( AppendEntriesResponse response)
        {
            var node = response.NodeId;
            //TODO: If there exists an N such that N > commitIndex, a majority of matchIndex[i] ≥ N, and log[N].term == currentTerm: set commitIndex = N (§5.3, §5.4)
            if (response.Success)
            {
                MatchIndex[node] = LastSentIndex[node];
                NextIndexes[node] = LastSentIndex[node] + 1;
                if (!Node.PersistentState.Log.Any()) 
                    return;

                for (var i = Node.CommitIndex+1; i <= MatchIndex.Values.Max(); i++)
                {
                    if (MatchIndex.Values.Count(mi => mi >= Node.CommitIndex) >= Node.Configuration.Majority &&
                        Node.PersistentState.Log[i].Term == Node.CurrentTerm)
                        Node.Commit(i);
                }
            }
            else
            {
                NextIndexes[node]--;
            }

        }
    }
}