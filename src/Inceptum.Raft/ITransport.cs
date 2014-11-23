using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    public interface ITransport<TCommand>
    {
        void Send<T>(Guid to, T message);
        IDisposable Subscribe<T>(Guid subscriberId, Action<T> handler);
 
    }
}