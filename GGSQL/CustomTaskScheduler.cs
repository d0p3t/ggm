using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GGSQL
{
    public sealed class CustomTaskScheduler : TaskScheduler, IDisposable
    {
        /// <summary>
        /// List to store all threads
        /// </summary>
        private List<Thread> threads = new List<Thread>();
        /// <summary>
        /// List to Store all Task Lists / Stacks
        /// </summary>
        private List<BlockingCollection<Task>> tasks = new List<BlockingCollection<Task>>();
        /// <summary>
        /// Number of Threads we will be using
        /// </summary>
        private int numberOfThreads = 1;
        /// <summary>
        /// An attribute to limit the usage of threads by the users, to avoid Deadlocks,
        /// because of bad querys and programming.
        /// </summary>
        public int ThreadLimit { set => numberOfThreads = GetNumberOfThreads(value); }

        /// <summary>
        /// Constructor
        /// </summary>
        public CustomTaskScheduler()
        {
            numberOfThreads = GetNumberOfThreads();
            for (int i = 0; i < numberOfThreads; i++)
            {
                tasks.Add(new BlockingCollection<Task>());
                ParameterizedThreadStart threadStart = new ParameterizedThreadStart(Execute);
                Thread thread = new Thread(threadStart);
                if (!thread.IsAlive)
                {
                    thread.Start(i);
                }
                threads.Add(thread);
            }
        }

        /// <summary>
        /// Will be called because of IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Return the amount of Threads we will use. Make this configurable in the future
        /// If we got 2 Logical CPUs, we want at least 2 Threads, but we leave one open
        /// therafter for the server thread
        /// </summary>
        /// <param name="threadLimit">Limit the amount of threads used</param>
        /// <returns></returns>
        private static int GetNumberOfThreads(int threadLimit = 0)
        {
            if (threadLimit < Environment.ProcessorCount && threadLimit > 0)
                return threadLimit;
            if (Environment.ProcessorCount > 2)
                return Environment.ProcessorCount - 1;
            return (Environment.ProcessorCount > 1) ? Environment.ProcessorCount : 1;
        }

        /// <summary>
        /// Keep looping the Execution of Tasks forever
        /// </summary>
        /// <param name="internalThreadId">The threadschedulers internal thread id</param>
        private void Execute(object internalThreadId)
        {
            foreach (Task task in tasks[(int)internalThreadId].GetConsumingEnumerable())
            {
                TryExecuteTask(task);
            }
        }

        /// <summary>
        /// Find the thread with the lowest amount of tasks and add the new task there
        /// </summary>
        /// <param name="task">Task that is supposed to be queued</param>
        protected override void QueueTask(Task task)
        {
            if (task != null)
            {
                int internalThreadId = 0;
                for (int i = 1; i < numberOfThreads; i++)
                {
                    if (tasks[i].Count < tasks[internalThreadId].Count)
                        internalThreadId = i;
                }
                tasks[internalThreadId].Add(task);
            }
        }

        /// <summary>
        /// Call to Dispose
        /// </summary>
        /// <param name="dispose">bool, if we want to dispose the scheduler</param>
        private void Dispose(bool dispose)
        {
            if (dispose)
            {
                for (int i = 0; i < numberOfThreads; i++)
                {
                    tasks[i].CompleteAdding();
                    tasks[i].Dispose();
                }
            }
        }

        /// <summary>
        /// Return a List of all Tasks currently still being handled
        /// </summary>
        /// <returns>The list of Tasks that are currently queued to be executed on all threads</returns>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            IEnumerable<Task> taskList = tasks[0].ToArray();
            for (int i = 1; i < numberOfThreads; i++)
            {
                taskList = taskList.Concat(tasks[i].ToArray());
            }
            return taskList;
        }

        /// <summary>
        /// We don't allow inline execution
        /// </summary>
        /// <param name="task">The task that should be executed inline</param>
        /// <param name="wasQueued">If the task was queued already by the taskscheduler</param>
        /// <returns>false, task was not executed inline</returns>
        protected override bool TryExecuteTaskInline(Task task, bool wasQueued)
        {
            return false;
        }
    }
}
