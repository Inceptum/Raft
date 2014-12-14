using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    /// <summary>
    /// Raft node persistent state
    /// </summary>
    public class PersistentState<TCommand>
    {
        private long m_CurrentTerm;
        private readonly List<ILogEntry<TCommand>> m_Log;

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

                VotedFor = null;
                m_CurrentTerm = value;
            }
        }

        /// <summary>
        /// Gets or sets the candidateId that received vote in current term (or null if none).
        /// </summary>
        /// <value>
        /// The candidateId voted for.
        /// </value>
        public string VotedFor { get; set; }

        /// <summary>
        /// Gets log entries; 
        /// each entry contains command for state machine, and term when entry was received by leader (first index is 1)
        /// </summary>
        /// <value>
        /// The log.
        /// </value>
        public IList<ILogEntry<TCommand>> Log
        {
            get { return new ReadOnlyCollection<ILogEntry<TCommand>>(m_Log); }
        }

        public PersistentState()
        {
            m_Log = new List<ILogEntry<TCommand>>();
        }

        public bool EntryTermMatches(int prevLogIndex, long prevLogTerm)
        {
            if (prevLogIndex < 0 && m_Log.Count == 0)
                return prevLogIndex == -1;

            if (prevLogIndex >= m_Log.Count)
                return false;

            return prevLogIndex == -1 || m_Log[prevLogIndex].Term == prevLogTerm;
        }

        public bool DeleteEntriesAfter(int prevLogIndex)
        {
            var index = prevLogIndex + 1;
            if (index >= m_Log.Count) 
                return false;
            m_Log.RemoveRange(index, m_Log.Count - index);
            return true;
        }


        public void Append(IEnumerable<ILogEntry<TCommand>> entries)
        {
            m_Log.AddRange(entries.Select(e => new LogEntry<TCommand>(e.Term, e.Command)));
        }

        public TaskCompletionSource<object> Append(TCommand command)
        {
            var logEntry = new LogEntry<TCommand>(CurrentTerm, command);
            m_Log.Add(logEntry);
            return logEntry.Completion;
        }

        public bool IsLogOlderOrEqual(long lastLogIndex, long lastLogTerm)
        {
            if (m_Log.Count == 0)
                return true;
            var lastEntry = m_Log[m_Log.Count - 1];
            return lastEntry.Term < lastLogTerm || (lastEntry.Term == lastLogTerm && lastLogIndex >= m_Log.Count - 1);
        }

        public IEnumerable<ILogEntry<TCommand>> GetRange(int index, int entriesCount)
        {
            return m_Log.GetRange(index, entriesCount);
        }
    }
}