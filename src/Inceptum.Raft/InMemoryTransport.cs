using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
    class ActionDisposable:IDisposable
    {
        private readonly Action m_Action;

        private ActionDisposable(Action action)
        {
            m_Action = action;
        }

        public static IDisposable Create(Action action)
        {
            return new ActionDisposable(action);
        }
        public void Dispose()
        {
            m_Action();
        }
    }

    public class InMemoryTransport<TCommand> : ITransport<TCommand>
    {
        readonly Dictionary<Tuple<Guid,Type>, Action<object>> m_Subscriptions = new Dictionary<Tuple<Guid, Type>, Action<object>>();

        public void Send<T>(Guid to, T message)
        {
            var key = Tuple.Create(to, typeof(T));
            m_Subscriptions[key](message);
        }

        public IDisposable Subscribe<T>(Guid subscriberId, Action<T> handler)
        {
            var key = Tuple.Create(subscriberId, typeof(T));
            m_Subscriptions.Add(key, m => handler((T) m));
            return ActionDisposable.Create(() => m_Subscriptions.Remove(key));
        }
    }
}