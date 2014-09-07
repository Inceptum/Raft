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
    public class NodeConfiguration
    {
        public int ElectionTimeout { get; set; } 
        public List<Guid> KnownNodes { get; set; } 
        public Guid NodeId { get; set; }

        public int Majority
        {
            get { return KnownNodes==null?0:KnownNodes.Count/2; }
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
        public long CommitIndex { get;  private set; }
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
            m_State = m_State = new Follower(this);
            m_WrokerThread = new Thread(worker);
            m_WrokerThread.Start();
            m_VoteSubscription = m_Transport.Subscribe(Id,voteHandler);
            m_AppendEntriesSubscription = m_Transport.Subscribe(Id, appendEntriesHandler);
        }

   
        private void worker(object obj)
        {
            int res = -1;
            while ((res= WaitHandle.WaitAny(new WaitHandle[] { m_Stop, m_Reset },m_Timeout) ) != 0 )
            {
                switch (res)
                {
                    case WaitHandle.WaitTimeout:
                        m_State.Timeout();                        
                        break;
                    case 1:
                        break;
                }
            }
        }

        public void ResetTimeout()
        {
            //TODO: random T , 2T
            m_Timeout = Configuration.ElectionTimeout;
            m_Reset.Set();
        }

        public void Timeout()
        {
            m_State.Timeout();
        }

        public void SwitchToCandidate()
        {
            m_State=new Candidate(this);

        }

        public long IncrementTerm()
        {
            //TODO: thread safety
           return  ++PersistentState.CurrentTerm;
        }

        public void SwitchToLeader()
        {
              m_State=new Leader(this);
        }

        public void SwitchToFollower()
        {
            m_State = new Follower(this);
        }


     

        private AppendEntriesResponse appendEntriesHandler(Guid nodeId, AppendEntriesRequest request)
        {
            if (request.Term > PersistentState.CurrentTerm)
            {
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower();
            }
            return m_State.AppendEntries(request);
        }

        private RequestVoteResponse voteHandler(Guid nodeId, RequestVoteRequest request)
        {
            if (request.Term > PersistentState.CurrentTerm)
            {
                PersistentState.CurrentTerm = request.Term;
                SwitchToFollower();
            }
            return m_State.RequestVote(request);
        }

        public void AppendEntries(Guid node, AppendEntriesRequest request)
        {
            m_Transport.Send(node, request, response => m_State.ProcessAppendEntries(node, response));
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
    }
}
