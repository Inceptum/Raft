using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Inceptum.Raft
{
    public class StateMachineHost:IDisposable
    {
        private readonly SingleThreadTaskScheduler m_StateMachineScheduler;
        private long m_LastApplied;
        private readonly object m_StateMachine;
        private readonly PersistentStateBase m_PersistentState;

        readonly Dictionary<Type, Action<object>> m_Handlers=new Dictionary<Type, Action<object>>();

        public long LastApplied
        {
            get { return Interlocked.Read(ref m_LastApplied); }
        }

        public StateMachineHost([NotNull] object stateMachine, string nodeId, PersistentStateBase persistentState)
        {
            if (stateMachine == null) throw new ArgumentNullException("stateMachine");
            m_PersistentState = persistentState;
            m_LastApplied = -1;
            m_StateMachine = stateMachine;
            m_StateMachineScheduler = new SingleThreadTaskScheduler(ThreadPriority.Normal, string.Format("Raft StateMachine Thread {0}", nodeId));
            wire();
            SupportedCommands = string.Join(",", m_Handlers.Keys.Select(t => t.Name));
        }

        public string SupportedCommands { get; private set; }

        private void wire()
        {

            var handleMethods = m_StateMachine.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Apply" &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Length == 1 &&
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m => new
                {
                    method = m,
                    commandType = m.GetParameters().First().ParameterType,
                });


            foreach (var method in handleMethods)
            {
                registerHandler(method.commandType);
            }
        }
        private void registerHandler(Type commandType)
        {
            var command = Expression.Parameter(typeof(object), "command");
            Expression[] parameters = { Expression.Convert(command, commandType) };
            var call = Expression.Call(Expression.Constant(m_StateMachine), "Apply", null, parameters);
            var lambda = (Expression<Action<object>>)Expression.Lambda(call, command);
            m_Handlers.Add(commandType,lambda.Compile());
        }
        public int Apply(int startIndex,int endIndex)
        {
            //TODO: exception handling. Crashed command processing should restart the node (https://groups.google.com/forum/#!searchin/raft-dev/state$20machinhe$20fault/raft-dev/6BauqBX6yEs/W6pZFdKcLckJ)
            //TODO: index should be long
            var processedIndex = startIndex - 1;
            for (var i = startIndex; i <= Math.Min(endIndex, m_PersistentState.Log.Count - 1); i++)
            {
                var logEntry=m_PersistentState.Log[i];
                var index = i;
                var handler = m_Handlers[logEntry.Command.GetType()];

                //TODO: crash if command is not supported !!!
                Task.Factory.StartNew(() =>
                {
                    handler(logEntry.Command);
                    logEntry.Completion.SetResult(null); //report completion
                    Interlocked.Exchange(ref m_LastApplied, index);
                }, CancellationToken.None, TaskCreationOptions.None, m_StateMachineScheduler);
                processedIndex = i;
            }
            return processedIndex;
        }

        public void Dispose()
        {
            m_StateMachineScheduler.Wait();
            m_StateMachineScheduler.Dispose();
        }
    }
}