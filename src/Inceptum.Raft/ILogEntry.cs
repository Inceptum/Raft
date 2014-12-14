using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft
{
    public interface ILogEntry<out TCommand>
    {
        /// <summary>
        /// Gets the term term when entry was received by leader.
        /// </summary>
        /// <value>
        /// The term.
        /// </value>
        long Term { get;  }
        /// <summary>
        /// Gets the command.
        /// </summary>
        /// <value>
        /// The command.
        /// </value>
        TCommand Command { get; }

        //TODO: I do not like string it here. LogEntry is about data only.
        TaskCompletionSource<object> Completion { get; }
  
    }
}