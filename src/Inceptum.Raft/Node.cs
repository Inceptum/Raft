﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        public static readonly StringBuilder m_Log = new StringBuilder();
        private readonly AutoResetEvent m_Reset = new AutoResetEvent(false);
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private readonly SingleThreadTaskScheduler m_Scheduler = new SingleThreadTaskScheduler(ThreadPriority.AboveNormal);
        private readonly IDisposable[] m_Subscriptions;
        private readonly object m_SyncRoot = new object();
        private readonly Thread m_TimeoutHandlingThread;
        private readonly ITransport m_Transport;
        private INodeState<TCommand> m_State;
        private int m_TimeoutBase;

        public NodeConfiguration Configuration { get; private set; }

        /// <summary>
        ///     Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        ///     The state of the persistent.
        /// </value>
        internal PersistentState<TCommand> PersistentState { get; private set; }

        /// <summary>
        ///     Gets the index of highest log entry known to be committed (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        ///     The index of the commit.
        /// </value>
        public int CommitIndex { get; internal set; }

        /// <summary>
        ///     Gets the index of highest log entry applied to statemachine (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        ///     The last applied  log entry index.
        /// </value>
        public long LastApplied { get; private set; }

        public string LeaderId { get; private set; }

        public string Id { get; private set; }

        public NodeState State
        {
            get { return m_State.State; }
        }

        public long CurrentTerm
        {
            get { return PersistentState.CurrentTerm; }
        }
       
        public IEnumerable<ILogEntry<TCommand>> LogEntries
        {
            get { return PersistentState.Log; }
        }

        public DateTime CurrentStateEnterTime
        {
            get { return m_State.EnterTime; }
        }

        public Node(PersistentState<TCommand> persistentState, NodeConfiguration configuration, ITransport transport)
        {
            m_Transport = transport;
            Id = configuration.NodeId;
            Configuration = configuration;
            PersistentState = persistentState;
            m_TimeoutHandlingThread = new Thread(timeoutHandlingLoop)
            {
                //Due to timeout logic it is significant to get execution context precisely
                Priority = ThreadPriority.AboveNormal
            };

            m_Subscriptions = new[]
            {
                subscribe<VoteRequest>(Handle),
                subscribe<VoteResponse>(Handle),
                subscribe<AppendEntriesRequest<TCommand>>(Handle),
                subscribe<AppendEntriesResponse>(Handle)
            };

            m_TimeoutBase = Configuration.ElectionTimeout;
            CommitIndex = -1;
            LastApplied = -1;
        }

        private IDisposable subscribe<T>(Action<T> handler)
        {
            return m_Transport.Subscribe<T>(
                Id,
                message => Task.Factory.StartNew(() =>
                {
                    lock (m_SyncRoot)
                    {
                        handler(message);
                    }
                }, CancellationToken.None, TaskCreationOptions.None, m_Scheduler));
        }

        public void Start()
        {
            SwitchToFollower(null);
            m_TimeoutHandlingThread.Start();
        }

        public void Apply(TCommand command)
        {
            lock(m_SyncRoot)
            {
                m_State.Apply(command);
            }
        }

        private void timeoutHandlingLoop(object obj)
        {
            Thread.CurrentThread.Name = Id.ToString();
            int res = -1;

            while (res != 0)
            {
                int timeoutBase = m_TimeoutBase;
                res = WaitHandle.WaitAny(new WaitHandle[] {m_Stop, m_Reset}, timeoutBase);
                //If CPU is loaded it is possible that after m_Reset is set, context would be swithed after timout has ended.
                res = m_Reset.WaitOne(0) ? 1 : res;

                if (res == WaitHandle.WaitTimeout)
                {
                    Log("Timeout reached ({0})", timeoutBase);
                    lock (m_SyncRoot)
                    {
                        m_State.Timeout();
                    }
                }
                else if (res == 1)
                {
                    Log("Timeout reset");
                }
                m_Reset.Reset();
            }
        }

        internal void ResetTimeout()
        {
            m_TimeoutBase = m_State.GetTimeout(Configuration.ElectionTimeout);
            Log("timeout reset for {0}", m_TimeoutBase);
            m_Reset.Set();
        }

        internal void SwitchToCandidate()
        {
            LeaderId = null;
            switchTo(new Candidate<TCommand>(this));
        }

        internal void SwitchToLeader()
        {
            LeaderId = Id;
            switchTo(new Leader<TCommand>(this));
        }

        internal void SwitchToFollower(string leaderId)
        {
            LeaderId = leaderId;
            switchTo(new Follower<TCommand>(this));
        }

        private void switchTo(INodeState<TCommand> state)
        {
            m_State = state;
            m_State.Enter();
        }


        internal void AppendEntries(string node, AppendEntriesRequest<TCommand> request)
        {
            m_Transport.Send(Id, node, request);
        }

        internal void RequestVotes()
        {
            var request = new VoteRequest
            {
                CandidateId = Id,
                Term = PersistentState.CurrentTerm,
                LastLogIndex = PersistentState.Log.Count - 1,
                LastLogTerm = PersistentState.Log.Select(l => l.Term).LastOrDefault()
            };

            foreach (var node in Configuration.KnownNodes)
            {
                m_Transport.Send(Id, node, request);
            }
        }

        internal void Commit(long leaderCommit)
        {
            Log("Got HB from leader:{0}", LeaderId);
            for (int i = CommitIndex + 1; i <= Math.Min(leaderCommit, PersistentState.Log.Count - 1); i++)
            {
                Log("APPLY: {0}", PersistentState.Log[i].Command);
                //TODO: actual commit logic
                Console.WriteLine(Id+"|"+CurrentTerm+" > APPLY: " + PersistentState.Log[i].Command);
                CommitIndex = i;
                LastApplied = i;
            }
        }

        internal long IncrementTerm()
        {
            PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
            return PersistentState.CurrentTerm;
        }

        #region Message Handlers

        internal void Handle(AppendEntriesRequest<TCommand> request)
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

            m_Transport.Send(Id, request.LeaderId, new AppendEntriesResponse
            {
                Success = m_State.Handle(request),
                Term = PersistentState.CurrentTerm,
                NodeId = Id
            });
        }

        internal void Handle(AppendEntriesResponse response)
        {
            if (response.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  node {2}. {0} -> {1}", PersistentState.CurrentTerm, response.Term, response.NodeId);
                PersistentState.CurrentTerm = response.Term;
                SwitchToFollower(null);
            }


            m_State.Handle(response);
        }

        internal void Handle(VoteRequest voteRequest)
        {
            if (voteRequest.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  candidate {2}. {0} -> {1}", PersistentState.CurrentTerm, voteRequest.Term, voteRequest.CandidateId);
                PersistentState.CurrentTerm = voteRequest.Term;
                SwitchToFollower(null);
            }

            bool granted = m_State.Handle(voteRequest);
            if (granted)
            {
                PersistentState.VotedFor = voteRequest.CandidateId;
                ResetTimeout();
            }

            m_Transport.Send(Id, voteRequest.CandidateId,
                new VoteResponse
                {
                    NodeId = Id,
                    Term = PersistentState.CurrentTerm,
                    VoteGranted = granted
                });
        }

        internal void Handle(VoteResponse vote)
        {
            if (vote.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  node {2}. {0} -> {1}", PersistentState.CurrentTerm, vote.Term, vote.NodeId);
                PersistentState.CurrentTerm = vote.Term;
                SwitchToFollower(null);
            }

            m_State.Handle(vote);
        }

        #endregion

        public void Dispose()
        {
            foreach (IDisposable subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }
            m_Stop.Set();
            m_TimeoutHandlingThread.Join();
            m_Scheduler.Wait();
        }

        public void Log(string format, params object[] args)
        {
            m_Log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff ") + string.Format(" {{{0:00}}}", Thread.CurrentThread.ManagedThreadId) + "> " + Id + "[" +
                             PersistentState.CurrentTerm + "]:" + string.Format(format, args));
        }
    }
}