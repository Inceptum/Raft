using System;
using System.Collections.Generic;

namespace Inceptum.Raft
{
    public class InMemoryPersistentState : PersistentStateBase
    {
        protected override Tuple<long, string> LoadState()
        {
            return Tuple.Create<long, string>(0, null);
        }

        protected override void SaveState(long currentTerm, string votedFor)
        {
            
        }

        protected override IEnumerable<LogEntry> LoadLog()
        {
            return new LogEntry[0];
        }

        protected override void RemoveLogStartingFrom(int index)
        {
             
        }

        protected override void AppendLog(params LogEntry[] logEntries)
        {
            
        }
    }
}