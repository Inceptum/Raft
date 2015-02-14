using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Logging;
using Inceptum.Raft.Rpc;
using Inceptum.Raft.States;

namespace Inceptum.Raft
{
    public enum NodeState
    {
        None,
        Leader,
        Candidate,
        Follower
    }

    //TODO: snapshots
    //TODO: logging
    //TODO: fluent configuration
    //TODO: Cluster membership changes

    //TODO: restart node on state machine crash (tests/refactoring)
    //TODO: command accepting from clients (tests)
    //TODO: real world transport (tests)
    //TODO[DONE]: get rid of <TCommand> 


    public class Node : IDisposable
    {
        private readonly AutoResetEvent m_ResetTimeout = new AutoResetEvent(false);
        private readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private SingleThreadTaskScheduler m_Scheduler;
        private IDisposable[] m_Subscriptions;
        private readonly object m_SyncRoot = new object();
        private Thread m_TimeoutHandlingThread;
        private readonly ITransport m_Transport;
        private INodeState m_State;
        private int m_TimeoutBase;
        private StateMachineHost m_StateMachineHost;
        private string m_LeaderId;
        private readonly Func<object> m_StateMachineFactory;
        private volatile bool m_IsStarted;
        internal ILogger Logger { get; private set; }


        public NodeConfiguration Configuration { get; private set; }

        /// <summary>
        ///     Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        ///     The state of the persistent.
        /// </value>
        internal PersistentStateBase PersistentState { get; private set; }

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

        public string LeaderId
        {
            get { return m_LeaderId; }
            private set
            {
                if(m_LeaderId==value)
                    return;
                m_LeaderId = value;
                Logger.Debug("Got new leader {0}", LeaderId);
            }
        }

        public string Id { get; private set; }

        public NodeState State
        {
            get { return m_State==null?NodeState.None: m_State.State; }
        }

        public long CurrentTerm
        {
            get { return PersistentState.CurrentTerm; }
        }

        public IEnumerable<LogEntry> LogEntries
        {
            get { return PersistentState.Log; }
        }

        public DateTime CurrentStateEnterTime
        {
            get { return m_State.EnterTime; }
        }


        public Node(string path, NodeConfiguration configuration, ITransport transport, Func<object> stateMachineFactory)
            : this(new FilePersistentState(path), configuration, transport, stateMachineFactory)
        {
            
        }

        public Node(PersistentStateBase persistentState, NodeConfiguration configuration, ITransport transport,Func<object> stateMachineFactory )
        {
            m_StateMachineFactory = stateMachineFactory;
            m_Transport = transport;
            Id = configuration.NodeId;
            Configuration = configuration;
            PersistentState = persistentState;
            m_TimeoutBase = Configuration.ElectionTimeout;
            Logger =new LoggerWrapper(this, Configuration.LoggerFactory(GetType()));
        }


        private void restart()
        {
            stop();
            Start();
        }

        private IDisposable subscribe<T>(Action<T> handler)
        {
            return m_Transport.Subscribe<T>(
                message =>
                {
                    Task.Factory.StartNew(() =>
                    {
                        lock (m_SyncRoot)
                        {
                            try
                            {
                                handler(message);
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(e, "Failed to handle {0}", typeof (T).Name);
                            }
                        }
                    }, CancellationToken.None, TaskCreationOptions.None, m_Scheduler);
                    return Task.FromResult<object>(null);
                });
        }

        public void Start()
        {
            switchToFollower(null);
            m_Stop.Reset();
            CommitIndex = -1;
            Logger.Info("Starting node {0}", Id);
            m_StateMachineHost = new StateMachineHost(m_StateMachineFactory(), Configuration.NodeId, PersistentState);
            Logger.Info("Supported commands: {0}", m_StateMachineHost.SupportedCommands);
            m_Scheduler = new SingleThreadTaskScheduler(ThreadPriority.AboveNormal, string.Format("Raft Message and Timeout Thread {0}", Id));
            m_TimeoutHandlingThread = new Thread(timeoutHandlingLoop)
            {
                //Due to timeout logic it is significant to get execution context precisely
                Priority = ThreadPriority.AboveNormal
            };

            var disposable = m_Transport.Subscribe<ApplyCommadRequest>(request =>
            {
                lock (m_SyncRoot)
                {
                    return m_State.Apply(request.Command);
                }
            });
            m_Subscriptions = new[]
            {
                subscribe<VoteRequest>(Handle),
                subscribe<VoteResponse>(Handle),
                subscribe<AppendEntriesRequest>(Handle),
                subscribe<AppendEntriesResponse>(Handle),
                disposable
            };
            m_IsStarted = true;
            m_TimeoutHandlingThread.Start();
        }

        public async Task<object> Apply(object command)
        {
            return  await m_State.Apply(command); 
            /*lock (m_SyncRoot)
            {

            }*/
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
                        Logger.Trace("Timeout elapsed ({0})", timeoutBase);
                        lock (m_SyncRoot)
                        {
                            try
                            {
                                m_State.Timeout();
                            }
                            catch (Exception e)
                            {
                                Logger.Debug(e, "Failed to process timeout");
                            }
                        }
                        break;
                    case 1:
                        Logger.Trace("Timeout reset");
                        break;
                }
                m_ResetTimeout.Reset();
            }
            Console.WriteLine("EXITING timout thread");
        }

        internal void ResetTimeout()
        {
            m_TimeoutBase = m_State.GetTimeout(Configuration.ElectionTimeout);
            Logger.Trace("timeout reset for {0}", m_TimeoutBase);
            m_ResetTimeout.Set();
        }

        internal void SwitchToCandidate()
        {
            LeaderId = null;
            switchTo(new Candidate(this));
        }

        internal void SwitchToLeader()
        {
            LeaderId = Id;
            switchTo(new Leader(this));
        }

        private void switchToFollower(string leaderId)
        {
            LeaderId = leaderId;
            if (State == NodeState.Follower) 
                return;

            Logger.Debug("Switching state to Follower");
            switchTo(new Follower(this));
        }

        private void switchTo(INodeState state)
        {
          
            m_State = state;
            m_State.Enter();
            Logger.Debug("Switched to {0}", State);
        }


        internal void AppendEntries(string node, AppendEntriesRequest request)
        {
            try
            {
                m_Transport.Send(node, request);
            }
            catch (Exception e)
            {
                Logger.Trace(e,"Failed to send AppendEntriesRequest message  to node {0} ", node);
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
                    Logger.Trace(e,"Failed to send VoteRequest message  to node {0}", node);
                }
            }
        }

        internal void Commit(long leaderCommit)
        {
            CommitIndex = m_StateMachineHost.Apply(CommitIndex + 1, (int) leaderCommit, e =>
            {
                Logger.Fatal(e,"Sate machine failed to process command. Restarting...");
                ThreadPool.QueueUserWorkItem(state => restart());
            });
        }

        internal long IncrementTerm()
        {
            PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
            return PersistentState.CurrentTerm;
        }

        #region Message Handlers

        internal void Handle(AppendEntriesRequest request)
        {
            if (!Configuration.KnownNodes.Contains(request.LeaderId))
            {
                Logger.Trace("Got AppendEntriesRequest from unknown node {0}. Ignoring...", request.LeaderId);
                return;
            }
            ResetTimeout();

            if (request.Term > PersistentState.CurrentTerm)
            {
                Logger.Debug("Got newer term {0} from leader {1}",  request.Term, request.LeaderId);
                PersistentState.CurrentTerm = request.Term;
            }

            if (request.Term >= PersistentState.CurrentTerm && (State != NodeState.Follower || LeaderId != request.LeaderId))
            {
                switchToFollower(request.LeaderId);
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
                Logger.Trace(e,"Failed to send AppendEntriesResponse message  to node {0}", request.LeaderId);
            }
            

        }

        internal void Handle(AppendEntriesResponse response)
        {

            if (!Configuration.KnownNodes.Contains(response.NodeId))
            {
                Logger.Trace("Got AppendEntriesResponse from unknown node {0}. Ignoring...", response.NodeId);
                return;
            }
            if (response.Term > PersistentState.CurrentTerm)
            {
                Logger.Debug("Got newer term {0} from node {1}", response.Term, response.NodeId);
                PersistentState.CurrentTerm = response.Term;
                switchToFollower(null);
            }
            m_State.Handle(response);


        }

        internal void Handle(VoteRequest voteRequest)
        {
            if (!Configuration.KnownNodes.Contains(voteRequest.CandidateId))
            {
                Logger.Trace("Got VoteRequest from unknown node {0}. Ignoring...", voteRequest.CandidateId);
                return;
            }
            if (voteRequest.Term > PersistentState.CurrentTerm)
            {
                Logger.Debug("Got newer term {0} from candidate {1}.", voteRequest.Term, voteRequest.CandidateId);
                PersistentState.CurrentTerm = voteRequest.Term;
                switchToFollower(null);
            }

            var granted = m_State.Handle(voteRequest);
            if (granted)
            {
                PersistentState.VotedFor = voteRequest.CandidateId;
                Logger.Debug("Voting for {0}", voteRequest.CandidateId);
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
                Logger.Trace(e,"Failed to send VoteResponse message  to node {0} ", voteRequest.CandidateId);
            }
        }

        internal void Handle(VoteResponse vote)
        {
            if (!Configuration.KnownNodes.Contains(vote.NodeId))
            {
                Logger.Trace("Got VoteResponse from unknown node {0}. Ignoring...", vote.NodeId);
                return;
            }

            if (vote.Term > PersistentState.CurrentTerm)
            {
                Logger.Debug("Got newer term {0} from node {1}", vote.Term, vote.NodeId);
                PersistentState.CurrentTerm = vote.Term;
                switchToFollower(null);
            }

            m_State.Handle(vote);
        }

        #endregion

        public void Dispose()
        {
            stop();
        }

        private void stop()
        {
            if(!m_IsStarted)
                return;
            m_IsStarted = false;
            Logger.Info("Stopping node {0}", Id);
            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }
            m_Subscriptions=new IDisposable[0];
            m_Stop.Set();
            m_TimeoutHandlingThread.Join();
            m_Scheduler.Wait();
            m_StateMachineHost.Dispose();
            PersistentState.Reload();
            Console.WriteLine("!!!");
        }

        public Task<object> SendCommandToLeader(object command)
        {
            if (LeaderId == null)
                return Task<object>.Factory.StartNew(() =>
                {
                    throw new InvalidOperationException("Can not process Command unless Leader is selected");
                });
            return m_Transport.Send(LeaderId, new ApplyCommadRequest { Command = command });
        }
    }
}