using System;
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

    //TODO: real world transport
    //TODO: command accepting from clients
    //TODO: logging
    //TODO: restart node on state machine crash
    //TODO: snapshots

    public class Node<TCommand> : IDisposable
    {
        public static readonly StringBuilder m_Log = new StringBuilder();
        private readonly AutoResetEvent m_ResetTimeout = new AutoResetEvent(false);
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private readonly SingleThreadTaskScheduler m_Scheduler;
        private readonly IDisposable[] m_Subscriptions;
        private readonly object m_SyncRoot = new object();
        private readonly Thread m_TimeoutHandlingThread;
        private readonly ITransport m_Transport;
        private INodeState<TCommand> m_State;
        private int m_TimeoutBase;
        private readonly StateMachineHost<TCommand> m_StateMachineHost;


        public NodeConfiguration Configuration { get; private set; }

        /// <summary>
        ///     Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        ///     The state of the persistent.
        /// </value>
        internal PersistentStateBase<TCommand> PersistentState { get; private set; }

        /// <summary>
        ///     Gets the index of highest log entry known to be committed (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        ///     The index of the commit.
        /// </value>
        public int CommitIndex { get; private set; }

        /// <summary>
        ///     Gets the index of highest log entry applied to statemachine (initialized to 0, increases monotonically)
        /// </summary>
        /// <value>
        ///     The last applied  log entry index.
        /// </value>
        public long LastApplied { get { return m_StateMachineHost.LastApplied; } }

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

        public IEnumerable<LogEntry<TCommand>> LogEntries
        {
            get { return PersistentState.Log; }
        }

        public DateTime CurrentStateEnterTime
        {
            get { return m_State.EnterTime; }
        }


        public Node(string path, NodeConfiguration configuration, ITransport transport, IStateMachine<TCommand> stateMachine)
            :this(new FilePersistentState<TCommand>(path),configuration,transport,stateMachine)
        {
            
        }
        public Node(PersistentStateBase<TCommand> persistentState, NodeConfiguration configuration, ITransport transport,IStateMachine<TCommand> stateMachine )
        {
            m_Scheduler = new SingleThreadTaskScheduler(ThreadPriority.AboveNormal, string.Format("Raft Message and Timeout Thread {0}", configuration.NodeId));
            m_StateMachineHost = new StateMachineHost<TCommand>(stateMachine, configuration.NodeId, persistentState);
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
        }

        private IDisposable subscribe<T>(Action<T> handler)
        {
            return m_Transport.Subscribe<T>(
                message => Task.Factory.StartNew(() =>
                {
                    lock (m_SyncRoot)
                    {
                        try{
                            handler(message);
                        }
                        catch (Exception e)
                        {
                            Log("Failed to nandle {0}: {1}",typeof(T).Name, e.ToString());
                        }
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
            Task<object> apply;
            lock (m_SyncRoot)
            {
                apply = m_State.Apply(command);
            }
            apply.Wait();
        }
 

        private void timeoutHandlingLoop(object obj)
        {
            Thread.CurrentThread.Name = Id;
            var res = -1;

            while (res != 0)
            {
                var timeoutBase = m_TimeoutBase;
                res = WaitHandle.WaitAny(new WaitHandle[] {m_Stop, m_ResetTimeout}, timeoutBase);
                //If CPU is loaded it is possible that after m_Reset is set, context would be swithed after timout has ended.
                res = m_ResetTimeout.WaitOne(0) ? 1 : res;

                switch (res)
                {
                    case WaitHandle.WaitTimeout:
                        Log("Timeout elapsed ({0})", timeoutBase);
                        lock (m_SyncRoot)
                        {
                            try
                            {
                                m_State.Timeout();
                            }
                            catch (Exception e)
                            {
                                Log("Failed to process timeout: {0}",e.ToString());
                            }
                        }
                        break;
                    case 1:
                        Log("Timeout reset");
                        break;
                }
                m_ResetTimeout.Reset();
            }
        }

        internal void ResetTimeout()
        {
            m_TimeoutBase = m_State.GetTimeout(Configuration.ElectionTimeout);
            Log("timeout reset for {0}", m_TimeoutBase);
            m_ResetTimeout.Set();
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
            try
            {
                m_Transport.Send(node, request);
            }
            catch (Exception e)
            {
                Log("Failed to send AppendEntriesRequest message  to node {0}: {1} ",  node, e.ToString());
            }
            
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
                try
                {
                    m_Transport.Send(node, request);
                }
                catch (Exception e)
                {
                    Log("Failed to send VoteRequest message  to node {0}: {1} ", node, e.ToString());
                }
            }
        }
        internal void Commit(long leaderCommit)
        {
            CommitIndex = m_StateMachineHost.Apply(CommitIndex + 1, (int) leaderCommit);
        }

        internal long IncrementTerm()
        {
            PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
            return PersistentState.CurrentTerm;
        }

        #region Message Handlers

        internal void Handle(AppendEntriesRequest<TCommand> request)
        {

            if (!Configuration.KnownNodes.Contains(request.LeaderId))
            {
                Log("Got AppendEntriesRequest from unknown node {0}. Ignoring...", request.LeaderId);
                return;
            }
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

            try
            {
                m_Transport.Send(request.LeaderId, new AppendEntriesResponse
                {
                    Success = m_State.Handle(request),
                    Term = PersistentState.CurrentTerm,
                    NodeId = Id
                });
            }
            catch (Exception e)
            {
                Log("Failed to send AppendEntriesResponse message  to node {0}: {1} ", request.LeaderId, e.ToString());
            }
            

        }

        internal void Handle(AppendEntriesResponse response)
        {

            if (!Configuration.KnownNodes.Contains(response.NodeId))
            {
                Log("Got AppendEntriesResponse from unknown node {0}. Ignoring...", response.NodeId);
                return;
            }
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
            if (!Configuration.KnownNodes.Contains(voteRequest.CandidateId))
            {
                Log("Got VoteRequest from unknown node {0}. Ignoring...", voteRequest.CandidateId);
                return;
            }
            if (voteRequest.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  candidate {2}. {0} -> {1}", PersistentState.CurrentTerm, voteRequest.Term, voteRequest.CandidateId);
                PersistentState.CurrentTerm = voteRequest.Term;
                SwitchToFollower(null);
            }

            var granted = m_State.Handle(voteRequest);
            if (granted)
            {
                PersistentState.VotedFor = voteRequest.CandidateId;
                ResetTimeout();
            }
            try
            {
                m_Transport.Send(voteRequest.CandidateId,
                new VoteResponse
                {
                    NodeId = Id,
                    Term = PersistentState.CurrentTerm,
                    VoteGranted = granted
                });
            }
            catch (Exception e)
            {
                Log("Failed to send VoteResponse message  to node {0}: {1} ", voteRequest.CandidateId, e.ToString());
            }
        }

        internal void Handle(VoteResponse vote)
        {
            if (!Configuration.KnownNodes.Contains(vote.NodeId))
            {
                Log("Got VoteResponse from unknown node {0}. Ignoring...", vote.NodeId);
                return;
            }

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
            m_StateMachineHost.Dispose();
        }
 

        public void Log(string format, params object[] args)
        {
            m_Log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff ") + string.Format(" {{{0:00}}}", Thread.CurrentThread.ManagedThreadId) + "> " + Id + "[" +
                             PersistentState.CurrentTerm + "]:" + string.Format(format, args));
#if DEBUG
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ") + string.Format(" {{{0:00}}}", Thread.CurrentThread.ManagedThreadId) + "> " + Id + "[" +
                             PersistentState.CurrentTerm + "]:" + string.Format(format, args));
#endif
        }
    }
}