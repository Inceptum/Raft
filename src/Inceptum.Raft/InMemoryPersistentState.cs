using System;
using System.Collections.Generic;

namespace Inceptum.Raft
{
    public class InMemoryPersistentState<TCommand> : PersistentStateBase<TCommand>
    {
        protected override Tuple<long, string> LoadState()
        {
            return Tuple.Create<long, string>(0, null);
        }

        protected override void SaveState(long currentTerm, string votedFor)
        {
            
        }

        protected override IEnumerable<LogEntry<TCommand>> LoadLog()
        {
            return new LogEntry<TCommand>[0];
        }

        protected override void RemoveLogAfter(int index)
        {
             
        }

        protected override void AppendLog(params LogEntry<TCommand>[] logEntries)
        {
            
        }
    }
}