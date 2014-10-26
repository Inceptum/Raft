using System;

namespace Inceptum.Raft.Rpc
{
    /// <summary>
    /// Request for RequestVote RPC. Invoked by candidates to gather votes (§5.2).
    /// </summary>
    public class RequestVoteRequest
    {
        /// <summary>
        /// Gets or sets the candidate’s term
        /// </summary>
        /// <value>
        /// The term.
        /// </value>
        public long Term { get; set; }

        /// <summary>
        /// Gets or sets the identifier of candidate requesting vote.
        /// </summary>
        /// <value>
        /// The candidate identifier.
        /// </value>
        public Guid CandidateId { get; set; }


        /// <summary>
        /// Gets or sets the index index of candidate’s last log entry (§5.4).
        /// </summary>
        /// <value>
        /// The index of the previous log.
        /// </value>
        public long LastLogIndex { get; set; }

        /// <summary>
        /// Gets or sets the term of candidate’s last log entry.
        /// </summary>
        /// <value>
        /// The previous log term.
        /// </value>
        public long LastLogTerm { get; set; }


    }
}