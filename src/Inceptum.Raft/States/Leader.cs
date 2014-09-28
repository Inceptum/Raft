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
        public Dictionary<Guid,long> NextIndexes { get; private set; }
        /// <summary>
        /// For each server, index of highest log entry known to be replicated on server (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the match.
        /// </value>
        public Dictionary<Guid,long> MatchIndex { get; private set; }

        public Leader(Node node)
            : base(node)
        {
            
        }

        public override void Enter()
        {
            throw new NotImplementedException();
        }

        public override void Timeout()
        {
            //Upon election: send initial empty AppendEntries RPCs (heartbeat) to each server; repeat during idle periods to prevent election timeouts (§5.2)
            Node.SendHeartBeats();
        }
        //TODO: If command received from client: append entry to local log, respond after entry applied to state machine (§5.3)
        public override bool RequestVote(RequestVoteRequest request)
        {
            throw new NotImplementedException();
        }

        public override void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            throw new NotImplementedException();
        }

        public override bool AppendEntries(AppendEntriesRequest request)
        {
            throw new NotImplementedException();
        }

        public override void ProcessAppendEntriesResponse(Guid node, AppendEntriesResponse appendEntriesResponse)
        {
            throw new NotImplementedException();
        }
    }
}