﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;
using Inceptum.Raft.States;

namespace Inceptum.Raft
{
    public enum NodeState
    {
        Leader,
        Candidate,
        Follower
    }
    public class Node<TCommand> : IDisposable
    {
        INodeState<TCommand> m_State;
        readonly Thread m_WrokerThread;
        readonly AutoResetEvent m_Reset = new AutoResetEvent(false);
        readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private int m_TimeoutBase;
        private readonly ITransport<TCommand> m_Transport;
        private readonly IDisposable[] m_Subscriptions;
        readonly Random m_Random = new Random();
        public static readonly StringBuilder m_Log = new StringBuilder();
        public NodeConfiguration Configuration { get; private set; }

        /// <summary>
        /// Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        /// The state of the persistent.
        /// </value>
        internal PersistentState<TCommand> PersistentState { get; private set; }
        /// <summary>
        /// Gets the index of highest log entry known to be committed (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The index of the commit.
        /// </value>
        public int CommitIndex { get; private set; }
        /// <summary>
        /// Gets the index of highest log entry applied to statemachine (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        /// The last applied  log entry index.
        /// </value>
        public long LastApplied { get; private set; }

        public Guid? LeaderId { get; private set; }

        public Guid Id { get; private set; }

        public NodeState State
        {
            get { return m_State.State; }
        }
        public long CurrentTerm { get { return PersistentState.CurrentTerm; } }
        public DateTime CurrentStateEnterTime { get { return m_State.EnterTime; } }

        //private readonly LimitedConcurrencyLevelTaskScheduler m_Scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
        private readonly TaskScheduler m_Scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);

        private object m_SyncRoot=new object();
        public Node(PersistentState<TCommand> persistentState, NodeConfiguration configuration, ITransport<TCommand> transport)
        {
            m_Transport = transport;
            Id = configuration.NodeId;
            Configuration = configuration;
            PersistentState = persistentState;
            m_WrokerThread = new Thread(worker)
            {
                Priority=ThreadPriority.AboveNormal
            };
          
            m_Subscriptions = new[]
            {
                subscribe<RequestVoteRequest>(voteRequestHandler),
                subscribe<RequestVoteResponse>(voteResponseHandler),
                subscribe<AppendEntriesRequest<TCommand>>(appendEntriesHandler),
                subscribe<AppendEntriesResponse>(appendEntriesResponseHandler)
            };

            m_TimeoutBase = Configuration.ElectionTimeout;
        }

        Stopwatch sw =Stopwatch.StartNew();
        private IDisposable subscribe<T>(Action<T> handler)
        {
            
            return m_Transport.Subscribe<T>(
                Id,
                message =>
                {
                   
                        // m_Log.AppendLine(string.Format("{0}\t{1}", sw.ElapsedMilliseconds, m_Scheduler.Count));
                        //   Console.WriteLine("!!! " + m_Scheduler.Count);
                        Task.Factory.StartNew(() =>
                        {
                            lock (m_SyncRoot)
                            {
                                handler(message);
                            }
                        }, CancellationToken.None, TaskCreationOptions.None, m_Scheduler);
                    
                });
        }

        public void Start()
        {
            SwitchToFollower(null);
            m_WrokerThread.Start();

        }

        private void worker(object obj)
        {
            Thread.CurrentThread.Name = Id.ToString();
            int res=-1;

            while (res != 0)
            {
                var timeoutBase = m_TimeoutBase;
                res = WaitHandle.WaitAny(new WaitHandle[] {m_Stop, m_Reset}, timeoutBase);
                //If CPU is loaded it is possible that after m_Reset is set, context would be swithed after timout has ended.
                res = m_Reset.WaitOne(0) ? 1 : res;

                if (res == WaitHandle.WaitTimeout)
                {
                    Log("Timeout reached ({0})", timeoutBase);
                    timeout();
                }
                else if(res==1)
                {
                    Log("Timeout reset");
                }
                m_Reset.Reset();
            }
        }

        private void timeout()
        {
            lock (m_SyncRoot)
            {
                m_State.Timeout();
            }
        }


        public void SwitchToCandidate()
        {
            LeaderId = null;
            switchTo(new Candidate<TCommand>(this));

        }

        public void SwitchToLeader()
        {
            LeaderId = Id;
            switchTo(new Leader<TCommand>(this));
        }

        public void SwitchToFollower(Guid? leaderId)
        {
            LeaderId = leaderId;
            switchTo(new Follower<TCommand>(this));
        }

        private void switchTo(INodeState<TCommand> state)
        {
            m_State = state;
            m_State.Enter();
        }


        public void AppendEntries(Guid node, AppendEntriesRequest<TCommand> request)
        {
            m_Transport.Send(node, request);
        }

        public void RequestVotes()
        {
            var request = new RequestVoteRequest
            {
                CandidateId = Id,
                Term = PersistentState.CurrentTerm,
                LastLogIndex = PersistentState.Log.Count - 1,
                LastLogTerm = PersistentState.Log.Select(l => l.Term).LastOrDefault()
            };

            foreach (var node in Configuration.KnownNodes)
            {
                m_Transport.Send(node, request);
            }
        }

        public void Commit(long leaderCommit)
        {
            Log("Got HB from leader:{0}", LeaderId);
            for (int i = CommitIndex + 1; i <= Math.Min(leaderCommit, PersistentState.Log.Count - 1); i++)
            {
                //TODO: actual commit logic
                Console.WriteLine("APPLY: " + PersistentState.Log[i].Command);
                CommitIndex = i;
                LastApplied = i;
            }
        }

        public long IncrementTerm()
        {
            //TODO: thread safety
            PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
            return PersistentState.CurrentTerm;
        }

        public void ResetTimeout()
        {
            m_TimeoutBase = m_State.GetTimeout(Configuration.ElectionTimeout);
            Log("timeout reset for {0}",m_TimeoutBase);
            m_Reset.Set();
        }

        public void Apply(TCommand command)
        {
            //TODO: proxy to leader
        }

        #region Message Handlers

        private void appendEntriesHandler(AppendEntriesRequest<TCommand> request)
        {
            ResetTimeout();

            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from leader {2}. {0} -> {1}", PersistentState.CurrentTerm, request.Term, request.LeaderId);
                PersistentState.CurrentTerm = request.Term;
            }
            if (request.Term >= PersistentState.CurrentTerm && (State != NodeState.Follower || LeaderId != request.LeaderId))
            {
                SwitchToFollower(request.LeaderId);
            }

            m_Transport.Send(request.LeaderId, new AppendEntriesResponse
            {
                Success = m_State.AppendEntries(request),
                Term = PersistentState.CurrentTerm,
                NodeId = Id
            });
        }

        private void voteRequestHandler(RequestVoteRequest request)
        {
            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  candidate {2}. {0} -> {1}", PersistentState.CurrentTerm, request.Term, request.CandidateId);
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower(null);
            }

            var granted = m_State.RequestVote(request);
            if (granted)
            {
                PersistentState.VotedFor = request.CandidateId;
                ResetTimeout();
            }

            m_Transport.Send(request.CandidateId,
                new RequestVoteResponse
                {
                    NodeId = Id,
                    Term = PersistentState.CurrentTerm,
                    VoteGranted = granted
                });
        }

        void appendEntriesResponseHandler(AppendEntriesResponse response)
        {
            m_State.ProcessAppendEntriesResponse(response);
        }
        void voteResponseHandler(RequestVoteResponse vote)
        {
            m_State.ProcessVote(vote);
        }

        #endregion


        public void Dispose()
        {
            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }
            m_Stop.Set();
            m_WrokerThread.Join();
        }


        public void Log(string format, params object[] args)
        {
             m_Log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff ") +string.Format(" {{{0:00}}}",Thread.CurrentThread.ManagedThreadId) + "> " + Id + "[" + PersistentState.CurrentTerm + "]:" + string.Format(format, args));
        }
    }
}
