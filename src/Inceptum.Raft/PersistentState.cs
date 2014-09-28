using System;
using System.Collections.Generic;

namespace Inceptum.Raft
{
    /// <summary>
    /// Raft node persistent state
    /// </summary>
    public class PersistentState
    {
        private long m_CurrentTerm;

        /// <summary>
        /// Gets or sets the current term.
        /// </summary>
        /// <value>
        /// The current term.
        /// </value>
        public long CurrentTerm
        {
            get { return m_CurrentTerm; }
            set
            {
                if (m_CurrentTerm == value)
                {
                    return;
                }

                VotedFor = default(Guid);
                m_CurrentTerm = value;
            }
        }

        /// <summary>
        /// Gets or sets the candidateId that received vote in current term (or null if none).
        /// </summary>
        /// <value>
        /// The candidateId voted for.
        /// </value>
        public Guid VotedFor { get; set; }
        /// <summary>
        /// Gets log entries; 
        /// each entry contains command for state machine, and term when entry was received by leader (first index is 1)
        /// </summary>
        /// <value>
        /// The log.
        /// </value>
        public List<ILogEntry<object>> Log { get; private set; }

        public PersistentState()
        {
            Log=new List<ILogEntry<object>>();
        }

        public bool EntryTermMatches(int prevLogIndex, long prevLogTerm)
        {
            if (prevLogIndex < 0 || prevLogIndex >= Log.Count)
                return false;
            return Log[prevLogIndex].Term == prevLogTerm;
        }

        public void DeleteEntriesAfter(int prevLogIndex)
        {
            var index = prevLogIndex + 1;
            if (index < Log.Count)
                Log.RemoveRange(index, Log.Count - index);
        }

        public void Append(IEnumerable<ILogEntry<object>> entries)
        {
            Log.AddRange(entries);
        }

        public bool IsLogOlderOrEqual(long lastLogIndex, long lastLogTerm)
        {
            if (Log.Count == 0)
                return true;
            var lastEntry = Log[Log.Count - 1];
            return lastEntry.Term < lastLogTerm ||(lastEntry.Term == lastLogTerm && lastLogIndex >= Log.Count-1);
        }
    }
}