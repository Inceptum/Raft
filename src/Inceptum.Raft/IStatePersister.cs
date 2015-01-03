using System;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Raft.Rpc;
using Microsoft.SqlServer.Server;

namespace Inceptum.Raft
{
    public interface IStatePersister<TCommand>
    {
        Tuple<long,string> GetState();
        void SaveState(long term, string votedFor);

        IEnumerable<ILogEntry<TCommand>> GetLog();
        void Append(params ILogEntry<TCommand>[] entries);
        void RemoveAfter(int index);
    }
}
