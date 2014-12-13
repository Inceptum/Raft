using System;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    class LogEntry<TCommand> : ILogEntry<TCommand>
    {
        public long Term { get; private set; }
        public TCommand Command { get; private set; }
        public LogEntry(long term, TCommand command)
        {
            Term = term;
            Command = command;
            Completion=new TaskCompletionSource<object>();
        }
        
        public TaskCompletionSource<object> Completion { get; private set; }

    }
}