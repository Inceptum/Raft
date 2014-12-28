using System.Collections.Generic;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    public interface IStatePersister<TCommand>
    {
        string GetVotedFor();
        void SaveVotedFor(string node);

        long GetCurrentTerm();
        void SaveCurrentTerm(long term);

        IEnumerable<ILogEntry<TCommand>> GetLog();
        void Append(params ILogEntry<TCommand>[] entries);
        void RemoveAfter(int index);
    }
}