using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
        private readonly IDisposable m_VoteSubscription;
        private readonly IDisposable m_AppendEntriesSubscription;
        readonly Random m_Random=new Random();
        public  static readonly StringBuilder m_Log =new StringBuilder();
        public  NodeConfiguration Configuration { get; private set; }
        
        /// <summary>
        /// Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        /// The state of the persistent.
        /// </value>
        internal PersistentState<TCommand> PersistentState { get; private set; }
        /// <summary>
        /// Gets the index of highest log entry known to be committed (initialized to 0, increases monotonically)        /// </summary>
        /// <value>
        /// The index of the commit.
        /// </value>
        public int CommitIndex { get;  private  set; }
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

        public Node(PersistentState<TCommand> persistentState, NodeConfiguration configuration, ITransport<TCommand> transport)
        {
            m_Transport = transport;
            Id = configuration.NodeId;
            Configuration = configuration;
            PersistentState = persistentState;
            m_WrokerThread = new Thread(worker);
            m_VoteSubscription = m_Transport.Subscribe(Id,voteHandler);
            m_AppendEntriesSubscription = m_Transport.Subscribe(Id, appendEntriesHandler);
            m_TimeoutBase = Configuration.ElectionTimeout;
        }

        public void Start()
        {
            SwitchToFollower(null);
            m_WrokerThread.Start();
        }

        public void Apply(TCommand command)
        {
            //TODO: proxy to leader
        }
   
        private void worker(object obj)
        {
            int res = -1;
            while ((res = WaitHandle.WaitAny(new WaitHandle[] { m_Stop, m_Reset }, (int)Math.Round((m_Random.NextDouble() + 1) * m_TimeoutBase))) != 0)
            {
                if (res == WaitHandle.WaitTimeout)
                    timeout();
            }
        }

//        [MethodImpl(MethodImplOptions.Synchronized)]
        private void timeout()
        {
            m_State.Timeout();
        }

        public void ResetTimeout(double? k=null)
        {
            if (k.HasValue)
            {
                m_TimeoutBase = (int) Math.Round(Configuration.ElectionTimeout*k.Value);
                return;
            }


            //random T , 2T
            var rndNum = new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8), System.Globalization.NumberStyles.HexNumber));
            int rnd = rndNum.Next(0, Configuration.ElectionTimeout);
            m_TimeoutBase = rnd + Configuration.ElectionTimeout;
                
            m_Reset.Set();

        }


        public long IncrementTerm()
        {
            //TODO: thread safety
            PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
            return  PersistentState.CurrentTerm;
        }

        public void SwitchToCandidate()
        {
            LeaderId = null;
            switchTo(new Candidate<TCommand>(this));

        }

        public void SwitchToLeader()
        {
            LeaderId = Id;
           switchTo(new Leader<TCommand>(this) );
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


     
  //      [MethodImpl(MethodImplOptions.Synchronized)]
        private AppendEntriesResponse appendEntriesHandler(Guid nodeId, AppendEntriesRequest<TCommand> request)
        {
            ResetTimeout();

            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from leader {2}. {0} -> {1}", PersistentState.CurrentTerm, request.Term, request.LeaderId);
                PersistentState.CurrentTerm = request.Term;
            }
            if (request.Term >= PersistentState.CurrentTerm && (State!=NodeState.Follower||LeaderId!=request.LeaderId))
            {
                    SwitchToFollower(request.LeaderId);
            }
   
            ResetTimeout();
          
            return new AppendEntriesResponse
            {
                Success = m_State.AppendEntries(request),
                Term = PersistentState.CurrentTerm,
                NodeId = this.Id
            };
        }

 //       [MethodImpl(MethodImplOptions.Synchronized)]
        private RequestVoteResponse voteHandler(Guid nodeId, RequestVoteRequest request)
        {
            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  candidate {2}. {0} -> {1}", PersistentState.CurrentTerm,request.Term,  request.CandidateId);
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower(null);
            }

            var granted = m_State.RequestVote(request);
            if (granted)
            {
                PersistentState.VotedFor = request.CandidateId;
                ResetTimeout();

            }
            return new RequestVoteResponse
            {
                Term = PersistentState.CurrentTerm,
                VoteGranted = granted
            };
        }

        public void AppendEntries(Guid node, AppendEntriesRequest<TCommand> request)
        {
            m_Transport.Send(node, request, response => m_State.ProcessAppendEntriesResponse(node, response));
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
                var nodeId = node;
                m_Transport.Send(node, request, response => m_State.ProcessVote(nodeId, response));
            }
        }



        public void Dispose()
        {
            m_AppendEntriesSubscription.Dispose();
            m_VoteSubscription.Dispose();
            m_Stop.Set();
            m_WrokerThread.Join();
        }

        public void Commit(long leaderCommit)
        {
            Log("Got HB from leader:{0}",LeaderId);
            for (int i = CommitIndex+1; i <=Math.Min(leaderCommit,PersistentState.Log.Count-1); i++)
            {
                //TODO: actual commit logic
                Console.WriteLine("APPLY: " + PersistentState.Log[i].Command);
                CommitIndex = i;
                LastApplied = i;
            }
        }


        public void Log(string format,params object[] args)
        {
             m_Log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff ") + "> " + Id + "[" + PersistentState.CurrentTerm + "]:" + string.Format(format, args));
        }
    }
}
