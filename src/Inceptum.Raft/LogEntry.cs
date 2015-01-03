using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    [Serializable]
    public class LogEntry<TCommand> : ILogEntry<TCommand>
    {
        [NonSerialized]
        private readonly TaskCompletionSource<object> m_Completion;
        public long Term { get; private set; }
        public TCommand Command { get; private set; }
        public LogEntry(long term, TCommand command)
        {
            Term = term;
            Command = command;
            m_Completion=new TaskCompletionSource<object>();
        }

        public TaskCompletionSource<object> Completion
        {
            get { return m_Completion; }
        }

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