using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    /// <summary>
    /// Raft node persistent state
    /// </summary>
    public abstract class PersistentStateBase<TCommand>
    {
        private long m_CurrentTerm;
        private readonly List<LogEntry<TCommand>> m_Log;
        private string m_VotedFor;

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

                SaveState(value, null);
                m_VotedFor = null;
                m_CurrentTerm = value;
            }
        }

        /// <summary>
        /// Gets or sets the candidateId that received vote in current term (or null if none).
        /// </summary>
        /// <value>
        /// The candidateId voted for.
        /// </value>
        public string VotedFor
        {
            get { return m_VotedFor; }
            set
            {
                SaveState(m_CurrentTerm, value);
                m_VotedFor = value;
            }
        }

        /// <summary>
        /// Gets log entries; 
        /// each entry contains command for state machine, and term when entry was received by leader (first index is 1)
        /// </summary>
        /// <value>
        /// The log.
        /// </value>
        public IList<LogEntry<TCommand>> Log
        {
            get { return new ReadOnlyCollection<LogEntry<TCommand>>(m_Log); }
        }

        protected PersistentStateBase()
        {
            m_Log = new List<LogEntry<TCommand>>(LoadLog());
            var  state=LoadState();
            m_CurrentTerm = state.Item1;
            m_VotedFor = state.Item2;
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
            RemoveLogAfter(index);
            m_Log.RemoveRange(index, m_Log.Count - index);
            return true;
        }

        public void Append(IEnumerable<LogEntry<TCommand>> entries)
        {
            var logEntries = entries.Select(e => new LogEntry<TCommand>(e.Term, e.Command)).ToArray();
            AppendLog(logEntries);
            m_Log.AddRange(logEntries);
        }

        public TaskCompletionSource<object> Append(TCommand command)
        {
            var logEntry = new LogEntry<TCommand>(CurrentTerm, command);
            AppendLog(logEntry);
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

        public IEnumerable<LogEntry<TCommand>> GetRange(int index, int entriesCount)
        {
            return   m_Log.GetRange(index, entriesCount);
        }



        protected abstract Tuple<long, string> LoadState();
        protected abstract void SaveState(long currentTerm, string votedFor);
        protected abstract IEnumerable<LogEntry<TCommand>> LoadLog();
        protected abstract void RemoveLogAfter(int index);
        protected abstract void AppendLog(params LogEntry<TCommand>[] logEntries);

    }
}