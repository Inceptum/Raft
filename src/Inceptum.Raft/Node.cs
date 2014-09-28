﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;
using Inceptum.Raft.States;

namespace Inceptum.Raft
{
    public class NodeConfiguration
    {
        public int ElectionTimeout { get; set; } 
        public List<Guid> KnownNodes { get; set; } 
        public Guid NodeId { get; set; }


        public NodeConfiguration(Guid nodeId, params Guid[] knownNodes)
        {
            NodeId = nodeId;
            KnownNodes = knownNodes.Where(n=>n!=nodeId).ToList();
        }

        public int Majority
        {
            get { return KnownNodes == null ? 0 : KnownNodes.Count / 2 + 1; }
        }
    }

    public class Node : IDisposable
    {
        INodeState m_State;
        internal  NodeConfiguration Configuration { get; private set; }

        /// <summary>
        /// Gets or sets the state of the persistent.
        /// </summary>
        /// <value>
        /// The state of the persistent.
        /// </value>
        internal PersistentState PersistentState{ get; private set; }
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

        public Guid Id { get; private set; }

        readonly Thread m_WrokerThread;
        readonly AutoResetEvent m_Reset = new AutoResetEvent(false);
        readonly ManualResetEvent m_Stop = new ManualResetEvent(false);
        private int m_Timeout;
        private readonly ITransport m_Transport;
        private readonly IDisposable m_VoteSubscription;
        private readonly IDisposable m_AppendEntriesSubscription;

        public Node(PersistentState persistentState, NodeConfiguration configuration,ITransport transport)
        {
            m_Transport = transport;
            Id = configuration.NodeId;
            Configuration = configuration;
            PersistentState = persistentState;
            //m_State = m_State = new Follower(this);
            m_WrokerThread = new Thread(worker);
            m_VoteSubscription = m_Transport.Subscribe(Id,voteHandler);
            m_AppendEntriesSubscription = m_Transport.Subscribe(Id, appendEntriesHandler);
            m_Timeout = (int)Math.Round((r.NextDouble() + 1) * Configuration.ElectionTimeout);
        }

        public void Start()
        {
            SwitchToFollower();
            m_WrokerThread.Start();
        }

   
        private void worker(object obj)
        {
            int res = -1;
            while ((res = WaitHandle.WaitAny(new WaitHandle[] { m_Stop, m_Reset }, m_Timeout)) != 0)
            {
                switch (res)
                {
                    case WaitHandle.WaitTimeout:
                        m_TimeoutWasReset = false;
                        timeout();                        
                        break;
                    case 1:
                        break;
                }
            }
        }

        private bool m_TimeoutWasReset = false;
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void timeout()
        {
            if(!m_TimeoutWasReset)
                m_State.Timeout();
        }

        Random r=new Random();
        public void ResetTimeout(double k=1)
        {
            //TODO: random T , 2T
            m_Timeout = (int) Math.Round((r.NextDouble()+1)*Configuration.ElectionTimeout*k);
            m_TimeoutWasReset = true;
            m_Reset.Set();

        }
     

        public void SwitchToCandidate()
        {
            m_State=new Candidate(this);
            m_State.Enter();

        }

        public long IncrementTerm()
        {
            //TODO: thread safety
           PersistentState.CurrentTerm = PersistentState.CurrentTerm + 1;
           return  PersistentState.CurrentTerm;
        }

        public void SwitchToLeader()
        {
            m_State = new Leader(this);
            m_State.Enter();
        }

        public void SwitchToFollower()
        {
            m_State = new Follower(this);
            m_State.Enter();
        }


     
        [MethodImpl(MethodImplOptions.Synchronized)]
        private AppendEntriesResponse appendEntriesHandler(Guid nodeId, AppendEntriesRequest request)
        {
            ResetTimeout();
            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from leader {2}. {0} -> {1}", PersistentState.CurrentTerm, request.Term, request.LeaderId);
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower();
            }
            ResetTimeout();

            return new AppendEntriesResponse
            {
                Success = m_State.AppendEntries(request),
                Term = PersistentState.CurrentTerm,
                NodeId = this.Id
            };
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private RequestVoteResponse voteHandler(Guid nodeId, RequestVoteRequest request)
        {
            if (request.Term > PersistentState.CurrentTerm)
            {
                Log("Got newer term from  candidate {2}. {0} -> {1}", PersistentState.CurrentTerm,request.Term,  request.CandidateId);
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower();
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

        public void AppendEntries(Guid node, AppendEntriesRequest request)
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
            for (int i = CommitIndex+1; i <=Math.Min(leaderCommit,PersistentState.Log.Count-1); i++)
            {
                //TODO: actual commit logic
                Console.WriteLine(PersistentState.Log[i].Command);
                CommitIndex = i;
                LastApplied = i;
            }
        }


       public  static readonly StringBuilder m_Log =new StringBuilder(); 
        public void Log(string format,params object[] args)
        {
             m_Log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff ") + "> " + Id + "[" + PersistentState.CurrentTerm + "]:" + string.Format(format, args));
       //     Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ") + "> " + Id + "[" + PersistentState.CurrentTerm + "]:" + string.Format(format, args));
        }
 
    }
}
