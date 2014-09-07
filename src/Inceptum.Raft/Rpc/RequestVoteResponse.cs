using System;

namespace Inceptum.Raft.Rpc
{
    /// <summary>
    /// Response for RequestVote RPC
    /// </summary>
    public class RequestVoteResponse
    {
        /// <summary>
        /// Gets or sets the currentTerm, for candidate to update itself.
        /// </summary>
        /// <value>
        /// The term.
        /// </value>
        public long Term { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vote is granted.
        /// </summary>
        /// <value>
        ///   <c>true</c> if candidate has received teh vote; otherwise, <c>false</c>.
        /// </value>
        public bool VoteGranted { get; set; }
    }
}