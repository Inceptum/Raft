using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    public class LogEntry<TCommand> : ILogEntry<TCommand>
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

        protected bool Equals(LogEntry<TCommand> other)
        {
            return Term == other.Term && EqualityComparer<TCommand>.Default.Equals(Command, other.Command);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LogEntry<TCommand>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Term.GetHashCode()*397) ^ EqualityComparer<TCommand>.Default.GetHashCode(Command);
            }
        }

        public static bool operator ==(LogEntry<TCommand> left, LogEntry<TCommand> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LogEntry<TCommand> left, LogEntry<TCommand> right)
        {
            return !Equals(left, right);
        }
    }
}