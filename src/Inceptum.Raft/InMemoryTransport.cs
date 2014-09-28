﻿using System;
using System.Collections.Generic;
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

    public class InMemoryTransport:ITransport
    {
        readonly Dictionary<Guid,Func<Guid, AppendEntriesRequest,AppendEntriesResponse>> m_AppendEntriesSubscriptions=new Dictionary<Guid, Func<Guid, AppendEntriesRequest, AppendEntriesResponse>>();
        readonly Dictionary<Guid, Func<Guid, RequestVoteRequest, RequestVoteResponse>> m_RequestVoteSubscriptions = new Dictionary<Guid, Func<Guid, RequestVoteRequest, RequestVoteResponse>>();
        public void Send(Guid id, AppendEntriesRequest request, Action<AppendEntriesResponse> callback)
        {
            Task.Factory.StartNew(() =>
            {
                Func<Guid, AppendEntriesRequest, AppendEntriesResponse> handler;
                if (m_AppendEntriesSubscriptions.TryGetValue(id, out handler))
                {
                    handler(id, request);
                }
            });
        }

        public void Send(Guid id, RequestVoteRequest request, Action<RequestVoteResponse> callback)
        {
            Task.Factory.StartNew(() =>
            {
                Func<Guid, RequestVoteRequest, RequestVoteResponse> handler;
                if (m_RequestVoteSubscriptions.TryGetValue(id, out handler))
                {
                    handler(id, request);
                }
            });
        }

        public IDisposable Subscribe(Guid id, Func<Guid, AppendEntriesRequest, AppendEntriesResponse> appendEntries)
        {
            m_AppendEntriesSubscriptions[id] = appendEntries;
            return ActionDisposable.Create(()=>m_AppendEntriesSubscriptions.Remove(id));
        }

        public IDisposable Subscribe(Guid id, Func<Guid, RequestVoteRequest, RequestVoteResponse> appendEntries)
        {
            m_RequestVoteSubscriptions[id] = appendEntries;
            return ActionDisposable.Create(() => m_RequestVoteSubscriptions.Remove(id));

        }
    }
}