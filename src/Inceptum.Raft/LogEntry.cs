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
        }
    }
}