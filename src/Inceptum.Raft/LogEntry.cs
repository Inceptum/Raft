using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    [Serializable]
    public class LogEntry
    {
        [NonSerialized]
        private readonly TaskCompletionSource<object> m_Completion;
        public long Term { get; private set; }
        public object Command { get; private set; }
        public LogEntry(long term, object command)
        {
            Term = term;
            Command = command;
            m_Completion=new TaskCompletionSource<object>();
        }

        public TaskCompletionSource<object> Completion
        {
            get { return m_Completion; }
        }

        protected bool Equals(LogEntry other)
        {
            return Term == other.Term && Equals(Command, other.Command);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LogEntry) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Term.GetHashCode()*397) ^ (Command != null ? Command.GetHashCode() : 0);
            }
        }

        public static bool operator ==(LogEntry left, LogEntry right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(LogEntry left, LogEntry right)
        {
            return !Equals(left, right);
        }
    }
}