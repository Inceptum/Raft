namespace Inceptum.Raft.Rpc
{
    /// <summary>
    /// Request for InstallSnapshot RPC. Invoked by leader to send chunks of a snapshot to a follower. 
    /// Leaders always send chunks in order.
    /// </summary>
    public class InstallSnapshotRequest
    {
        /// <summary>
        ///     Gets or sets the leader’s term.
        /// </summary>
        /// <value>
        ///     The term.
        /// </value>
        public long Term { get; set; }


        /// <summary>
        ///     Gets or sets the leader identifier.
        /// </summary>
        /// <value>
        ///     The leader identifier.
        /// </value>
        public string LeaderId { get; set; }

        /// <summary>
        ///     Gets or sets the last index included by snapshot. The snapshot replaces all entries up through and including this
        ///     index.
        /// </summary>
        /// <value>
        ///     The last included index.
        /// </value>
        public int LastIncludedIndex { get; set; }

        /// <summary>
        ///     Gets or sets the term of LastIncludedIndex.
        /// </summary>
        /// <value>
        ///     The term of last included index.
        /// </value>
        public int LastIncludedTerm { get; set; }

        /// <summary>
        ///     Gets or sets the byte offset where chunk is positioned in the snapshot file.
        /// </summary>
        /// <value>
        ///     The offset.
        /// </value>
        public long Offset { get; set; }

        /// <summary>
        ///     Gets or sets raw bytes of the snapshot chunk, starting at offset.
        /// </summary>
        /// <value>
        ///     The data.
        /// </value>
        public byte[] Data { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether this is the last chunk.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this is the last chunk; otherwise, <c>false</c>.
        /// </value>
        public bool Done { get; set; }
    }
}