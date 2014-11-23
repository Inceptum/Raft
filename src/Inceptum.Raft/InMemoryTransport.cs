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
        Dictionary<Guid, LimitedConcurrencyLevelTaskScheduler> m_Schedulers = new Dictionary<Guid, LimitedConcurrencyLevelTaskScheduler>();
        readonly Dictionary<Guid, Func<Guid, AppendEntriesRequest<TCommand>, AppendEntriesResponse>> m_AppendEntriesSubscriptions = new Dictionary<Guid, Func<Guid, AppendEntriesRequest<TCommand>, AppendEntriesResponse>>();
        readonly Dictionary<Guid, Func<Guid, RequestVoteRequest, RequestVoteResponse>> m_RequestVoteSubscriptions = new Dictionary<Guid, Func<Guid, RequestVoteRequest, RequestVoteResponse>>();

        public InMemoryTransport(params Guid[] ids)
        {
            foreach (var guid in ids)
            {
                getScheduler(guid);
            }
        }

        public void Send(Guid id, AppendEntriesRequest<TCommand> request, Action<AppendEntriesResponse> callback)
        {
            Func<Guid, AppendEntriesRequest<TCommand>, AppendEntriesResponse> handler;
            if (m_AppendEntriesSubscriptions.TryGetValue(id, out handler))
            {
                Task.Factory.StartNew(() =>
                {
                    handler(id, request);
                }, CancellationToken.None, TaskCreationOptions.None, getScheduler(id));
            }
        }

        public void Send(Guid id, RequestVoteRequest request, Action<RequestVoteResponse> callback)
        {
            Func<Guid, RequestVoteRequest, RequestVoteResponse> handler;
            if (m_RequestVoteSubscriptions.TryGetValue(id, out handler))
            {
                Task.Factory.StartNew(() =>
                {

                    var response = handler(id, request);
                    callback(response);
                }, CancellationToken.None, TaskCreationOptions.None, getScheduler(id));
            }
        }

        private TaskScheduler getScheduler(Guid id)
        {
            LimitedConcurrencyLevelTaskScheduler s;
            if (!m_Schedulers.TryGetValue(id, out s))
            {
                s= new LimitedConcurrencyLevelTaskScheduler(1);
                m_Schedulers[id] = s;
            }
            return s;
        }

        public IDisposable Subscribe(Guid id, Func<Guid, AppendEntriesRequest<TCommand>, AppendEntriesResponse> appendEntries)
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