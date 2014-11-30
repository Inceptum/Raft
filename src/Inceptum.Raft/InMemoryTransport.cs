using System;
using System.Collections.Generic;

namespace Inceptum.Raft
{
    public class InMemoryTransport : ITransport 
    {
        readonly Dictionary<Tuple<string, Type>, Action<object>> m_Subscriptions = new Dictionary<Tuple<string, Type>, Action<object>>();
        readonly List<string> m_FailedNodes = new List<string>();
        public void EmulateConnectivityIssue(string nodeId)
        {
            m_FailedNodes.Add(nodeId);
        }
        public void RestoreConnectivity(string nodeId)
        {
            m_FailedNodes.Remove(nodeId);
        }


        public void Send<T>(string from, string to, T message)
        {
            if(m_FailedNodes.Contains(to))
                return;
            if (m_FailedNodes.Contains(from))
                return;
            var key = Tuple.Create(to, typeof(T));
            lock (m_Subscriptions)
            {
                Action<object> handler;
                if(m_Subscriptions.TryGetValue(key, out handler))
                    handler(message);
            }
        }

        public IDisposable Subscribe<T>(string subscriberId, Action<T> handler)
        {
            var key = Tuple.Create(subscriberId, typeof(T));
            lock (m_Subscriptions)
            {
                m_Subscriptions.Add(key, m => handler((T) m));
            }
            return ActionDisposable.Create(() =>
            {
                lock (m_Subscriptions)
                {
                    m_Subscriptions.Remove(key);
                }
            });
        }
    }
}