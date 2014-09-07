using System;
using System.Collections.Generic;
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
            //Sennd heartbeat
        }

        public override RequestVoteResponse RequestVote(RequestVoteRequest request)
        {
            throw new NotImplementedException();
        }

        public override void ProcessVote(Guid node, RequestVoteResponse vote)
        {
            throw new NotImplementedException();
        }

        public override AppendEntriesResponse AppendEntries(AppendEntriesRequest request)
        {
            throw new NotImplementedException();
        }

        public override void ProcessAppendEntries(Guid node, AppendEntriesResponse appendEntriesResponse)
        {
            throw new NotImplementedException();
        }
    }
}