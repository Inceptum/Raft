using System;
using System.Collections.Generic;
using System.IO;
using Inceptum.Raft;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
      public interface IInMemoryBus
    {
        void Send<T>(string from, string to, T message);
        IDisposable Subscribe<T>(string subscriberId, Action<T> handler);
    }
 
    public class InMemoryBus:IInMemoryBus
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
            if (m_FailedNodes.Contains(to))
                return;
            if (m_FailedNodes.Contains(from))
                return;
            var key = Tuple.Create(to, typeof(T));
            lock (m_Subscriptions)
            {
                Action<object> handler;
                if (m_Subscriptions.TryGetValue(key, out handler))
                    handler(message);
            }
        }

        public IDisposable Subscribe<T>(string subscriberId, Action<T> handler)
        {
            var key = Tuple.Create(subscriberId, typeof(T));
            lock (m_Subscriptions)
            {
                m_Subscriptions.Add(key, m => handler((T)m));
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

    public class InMemoryTransport : ITransport
    {
        private string m_NodeId;
        private static IInMemoryBus m_DefaultBus=new InMemoryBus();
        private static IInMemoryBus m_Bus;

        public InMemoryTransport(string nodeId,IInMemoryBus bus=null)
        {
            m_Bus = bus ?? m_DefaultBus;
            m_NodeId = nodeId;
        }
 
        public void Send(string to, AppendEntriesRequest message)
        {
            m_Bus.Send(m_NodeId, to, message);
        }

        public void Send(string to, AppendEntriesResponse message)
        {
            m_Bus.Send(m_NodeId, to, message);
        }

        public void Send(string to, VoteRequest message)
        {
            m_Bus.Send(m_NodeId, to, message);
        }

        public void Send(string to, VoteResponse message)
        {
            m_Bus.Send(m_NodeId, to, message);
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
           return m_Bus.Subscribe(m_NodeId, handler);
        }
    }

 
}