using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Inceptum.Raft;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft
{
      public interface IInMemoryBus
    {
          Task<object> Send<T>(string from, string to, T message);
          IDisposable Subscribe<T>(string subscriberId, Func<T, Task<object>> handler);
    }
 
    public class InMemoryBus:IInMemoryBus
    {
        readonly Dictionary<Tuple<string, Type>, Func<object, Task<object>>> m_Subscriptions = new Dictionary<Tuple<string, Type>, Func<object, Task<object>>>();
        readonly List<string> m_FailedNodes = new List<string>();
        public void EmulateConnectivityIssue(string nodeId)
        {
            m_FailedNodes.Add(nodeId);
        }
        public void RestoreConnectivity(string nodeId)
        {
            m_FailedNodes.Remove(nodeId);
        }


        public Task<object> Send<T>(string from, string to, T message)
        {
            if (m_FailedNodes.Contains(to))
                return Task.FromResult(default(object));
            if (m_FailedNodes.Contains(from))
                return Task.FromResult(default(object));
            var key = Tuple.Create(to, typeof(T));
            lock (m_Subscriptions)
            {
                Func<object, Task<object>> handler;
                if (m_Subscriptions.TryGetValue(key, out handler))
                   return handler(message);
            }
            return Task.FromResult(default(object));
        }

        public IDisposable Subscribe<T>(string subscriberId, Func<T, Task<object>> handler)
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
        private IInMemoryBus m_Bus;


        public string[] KnownNodes { get; private set; }

        public InMemoryTransport(string nodeId,IInMemoryBus bus, params string[] knownNodes)
        {
            KnownNodes = knownNodes;
            m_Bus = bus;
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

        public async Task<object> Send(string to, ApplyCommadRequest message)
        {
            return await m_Bus.Send(m_NodeId, to, message);
        }

        public IDisposable Subscribe<T>(Func<T, Task<object>> handler)
        {
            return m_Bus.Subscribe(m_NodeId,handler);
        }
    }

 
}