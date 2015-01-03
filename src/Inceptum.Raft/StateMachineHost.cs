using System;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    public class StateMachineHost<TCommand>
    {
        private readonly SingleThreadTaskScheduler m_StateMachineScheduler;
        private long m_LastApplied;
        private readonly IStateMachine<TCommand> m_StateMachine;
        private readonly PersistentStateBase<TCommand> m_PersistentState;

        public long LastApplied
        {
            get { return Interlocked.Read(ref m_LastApplied); }
        }

        public StateMachineHost(IStateMachine<TCommand> stateMachine, string nodeId, PersistentStateBase<TCommand> persistentState)
        {
            m_PersistentState = persistentState;
            m_LastApplied = -1;
            m_StateMachine = stateMachine;
            m_StateMachineScheduler = new SingleThreadTaskScheduler(ThreadPriority.Normal, string.Format("Raft StateMachine Thread {0}", nodeId));
        }

        public int Apply(int startIndex,int endIndex)
        {
            //TODO: exception handling. Crashed command processing should crash the node (https://groups.google.com/forum/#!searchin/raft-dev/state$20machinhe$20fault/raft-dev/6BauqBX6yEs/W6pZFdKcLckJ)
            //TODO: index should be long
            var processedIndex = startIndex - 1;
            for (var i = startIndex; i <= Math.Min(endIndex, m_PersistentState.Log.Count - 1); i++)
            {
                var logEntry=m_PersistentState.Log[i];
                var index = i;
                Task.Factory.StartNew(() =>
                {
                    m_StateMachine.Apply(logEntry.Command);
                    logEntry.Completion.SetResult(null); //report completion
                    Interlocked.Exchange(ref m_LastApplied, index);
                }, CancellationToken.None, TaskCreationOptions.None, m_StateMachineScheduler);
                processedIndex = i;
            }
            return processedIndex;
        }
    }
}