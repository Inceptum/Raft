using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    /// <summary>
    ///     Represents a <see cref="TaskScheduler"/> which executes code on a dedicated, single thread.
    /// </summary>
    /// <remarks>
    ///     You can use this class if you want to perform operations on a non thread-safe library from a multi-threaded environment.
    /// </remarks>
    public sealed class SingleThreadTaskScheduler
        : TaskScheduler, IDisposable
    {
        private readonly Thread m_Thread;
        private readonly CancellationTokenSource m_CancellationToken;
        private readonly BlockingCollection<Task> m_Tasks;


        /// <summary>
        /// Initializes a new instance of the <see cref="SingleThreadTaskScheduler" /> passsing an initialization action, optionally setting an <see cref="System.Threading.ApartmentState" />.
        /// </summary>
        /// <param name="threadPriority">The thread priority.</param>
        public SingleThreadTaskScheduler(ThreadPriority threadPriority)
        {
            m_CancellationToken = new CancellationTokenSource();
            m_Tasks = new BlockingCollection<Task>();
            m_Thread = new Thread(threadStart)
            {
                IsBackground = true, 
                Priority = threadPriority
            };

            m_Thread.Start();
        }


        /// <summary>
        ///     Waits until all scheduled <see cref="Task"/>s on this <see cref="SingleThreadTaskScheduler"/> have executed and then disposes this <see cref="SingleThreadTaskScheduler"/>.
        /// </summary>
        /// <remarks>
        ///     Calling this method will block execution. It should only be called once.
        /// </remarks>
        /// <exception cref="TaskSchedulerException">
        ///     Thrown when this <see cref="SingleThreadTaskScheduler"/> already has been disposed by calling either <see cref="Wait"/> or <see cref="Dispose"/>.
        /// </exception>
        public void Wait()
        {
            if (m_CancellationToken.IsCancellationRequested)
                throw new TaskSchedulerException("Cannot wait after disposal.");

            m_Tasks.CompleteAdding();
            m_Thread.Join();
            m_CancellationToken.Cancel();
        }

        /// <summary>
        ///     Disposes this <see cref="SingleThreadTaskScheduler"/> by not accepting any further work and not executing previously scheduled tasks.
        /// </summary>
        /// <remarks>
        ///     Call <see cref="Wait"/> instead to finish all queued work. You do not need to call <see cref="Dispose"/> after calling <see cref="Wait"/>.
        /// </remarks>
        public void Dispose()
        {
            if (m_CancellationToken.IsCancellationRequested)
                return;

            m_Tasks.CompleteAdding();
            m_CancellationToken.Cancel();
        }

        protected override void QueueTask(Task task)
        {
            verifyNotDisposed();

            m_Tasks.Add(task, m_CancellationToken.Token);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            verifyNotDisposed();

            if (m_Thread != Thread.CurrentThread)
                return false;
            if (m_CancellationToken.Token.IsCancellationRequested)
                return false;

            TryExecuteTask(task);
            return true;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            verifyNotDisposed();

            return m_Tasks.ToArray();
        }

        private void threadStart()
        {
            try
            {
                var token = m_CancellationToken.Token;
                foreach (var task in m_Tasks.GetConsumingEnumerable(token))
                    TryExecuteTask(task);
            }
            finally
            {
                m_Tasks.Dispose();
            }
        }

        private void verifyNotDisposed()
        {
            if (m_CancellationToken.IsCancellationRequested)
                throw new ObjectDisposedException(typeof(SingleThreadTaskScheduler).Name);
        }
    }
}