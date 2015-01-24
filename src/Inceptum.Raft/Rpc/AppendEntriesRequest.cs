using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Inceptum.Raft.Rpc
{
    /// <summary>
    /// Request for AppendEntries RPC. Invoked by leader to replicate log entries (§5.3); also used as heartbeat (§5.2)
    /// </summary>
    public class AppendEntriesRequest<TCommand>
    {
        /// <summary>
        /// Gets or sets the leader’s term.
        /// </summary>
        /// <value>
        /// The term.
        /// </value>
        public long Term { get; set; }

        /// <summary>
        /// Gets or sets the leader identifier.
        /// </summary>
        /// <value>
        /// The leader identifier.
        /// </value>
        public string LeaderId { get; set; }

        /// <summary>
        /// Gets or sets the index of log entry immediately preceding new ones.
        /// </summary>
        /// <value>
        /// The index of the previous log.
        /// </value>
        public int PrevLogIndex { get; set; }

        /// <summary>
        /// Gets or sets the term of PrevLogIndex entry.
        /// </summary>
        /// <value>
        /// The previous log term.
        /// </value>
        public long PrevLogTerm { get; set; }

        /// <summary>
        /// Gets or sets the log entries to store (empty for heartbeat; may send more than one for efficiency).
        /// </summary>
        /// <value>
        /// The entries.
        /// </value>
        [JsonIgnore]
        public IEnumerable<LogEntry<TCommand>> Entries { get; set; }

        /// <summary>
        /// Gets or sets the leader’s commit index.
        /// </summary>
        /// <value>
        /// The leader’s commit index.
        /// </value>
        public long LeaderCommit { get; set; }

    }
}