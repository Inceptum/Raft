using System;

namespace Inceptum.Raft.Rpc
{
    /// <summary>
    /// Response for AppendEntries RPC
    /// </summary>
    public class AppendEntriesResponse
    {
        /// <summary>
        /// Gets or sets the term currentTerm, for leader to update itself.
        /// </summary>
        /// <value>
        /// The term.
        /// </value>
        public long Term { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tthe follower contained entry matching prevLogIndex and prevLogTerm.
        /// </summary>
        /// <value>
        ///   <c>true</c> if ollower contained entry matching prevLogIndex and prevLogTerm; otherwise, <c>false</c>.
        /// </value>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the node identifier.
        /// </summary>
        /// <value>
        /// The node identifier.
        /// </value>
        public string NodeId { get; set; }
    }
}