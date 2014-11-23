using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{

    // Provides a task scheduler that ensures a maximum concurrency level while  
    // running on top of the thread pool. 
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool m_CurrentThreadIsProcessingItems;

        // The list of tasks to be executed  
        private readonly LinkedList<Task> m_Tasks = new LinkedList<Task>(); // protected by lock(_tasks) 

        // The maximum concurrency level allowed by this scheduler.  
        private readonly int m_MaxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items.  
        private int m_DelegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism.  
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            m_MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler.  
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough  
            // delegates currently queued or running to process tasks, schedule another.  
            lock (m_Tasks)
            {
                m_Tasks.AddLast(task);
                if (m_DelegatesQueuedOrRunning < m_MaxDegreeOfParallelism)
                {
                    ++m_DelegatesQueuedOrRunning;
                    notifyThreadPoolOfPendingWork();
                }
            }
        }

        public long Count
        {
            get { return m_Tasks.Count; }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler.  
        private void notifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items. 
                // This is necessary to enable inlining of tasks into this thread.
                m_CurrentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue. 
                    while (true)
                    {
                        Task item;
                        lock (m_Tasks)
                        {
                            // When there are no more items to be processed, 
                            // note that we're done processing, and get out. 
                            if (m_Tasks.Count == 0)
                            {
                                --m_DelegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = m_Tasks.First.Value;
                            m_Tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue 
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread 
                finally { m_CurrentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread.  
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining 
            if (!m_CurrentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue 
            if (taskWasPreviouslyQueued)
                // Try to run the task.  
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler.  
        protected sealed override bool TryDequeue(Task task)
        {
            lock (m_Tasks) return m_Tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler.  
        public sealed override int MaximumConcurrencyLevel { get { return m_MaxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler.  
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(m_Tasks, ref lockTaken);
                if (lockTaken) return m_Tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(m_Tasks);
            }
        }
    }
}
