using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Async;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.Queue.InProcessQueue
{
	public class InProcessQueue : IQueue, IQueueEntry, IQueueExit
    {
		public IQueueEntry Entry => this;
	    public IQueueExit Exit => this;
		/// <summary>
		/// risen, when a message has been inserted into the queue
		/// </summary>
		private readonly AsyncEvent _messageInsertedEv;

		/// <summary>
		/// for each message priority one message queue.
		/// Sorted by priority.
		/// </summary>
		private readonly SortedDictionary<byte,Queue<object>> _queues = new SortedDictionary<byte,Queue<object>>();
	    private readonly object _lock = new object();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cancellationToken"></param>
		public InProcessQueue(CancellationToken cancellationToken)
		{
			_messageInsertedEv = new AsyncEvent(cancellationToken);
		}

		public void Clear()
		{
			lock (_lock)
			{
				_queues.Clear();
			}
		}

#		pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		async Task IQueueEntry.Enqueue(object msg)
#		pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			byte prio = (msg as IPrioMessage)?.Priority ?? 200;

			lock (_lock)
			{
				GetSafeQueue(prio).Enqueue(msg);
			}

			_messageInsertedEv.Rise();
		}

		/// <summary>
		/// gets message queue of given message priority.
		/// Must be called in locked state !!
		/// Creates the message queue, if not exists
		/// </summary>
		/// <param name="prio"></param>
		/// <returns></returns>
		private Queue<object> GetSafeQueue(byte prio)
		{
			Queue<object> queue;
			if (!_queues.TryGetValue(prio, out queue))
			{
				queue = new Queue<object>();
				_queues[prio] = queue;
			}
			return queue;
		}


		async Task<object> IQueueExit.Dequeue()
		{
			while (true)
			{
				Task waitForInsertion;

				lock (_lock)
				{
					var queue = _queues.FirstOrDefault(kv => kv.Value.Count != 0).Value;

					if (queue != null)
					{
						var msg = queue.Dequeue();
						return msg;
					}

					waitForInsertion = _messageInsertedEv.RegisterForEvent();
				}

				await waitForInsertion;
			}
		}

		async Task<TimeoutResult<object>> IQueueExit.Dequeue(int timeoutMs)
		{
			while (true)
			{
				Task waitForInsertion;

				lock (_lock)
				{
					var queue = _queues.FirstOrDefault(kv => kv.Value.Count != 0).Value;

					if (queue != null)
					{
						var msg = queue.Dequeue();
						return new TimeoutResult<object>(msg,false);
					}

					waitForInsertion = _messageInsertedEv.RegisterForEvent();
				}

				var timedout = Task.Delay(timeoutMs);
				await Task.WhenAny(waitForInsertion, timedout);
				if (!waitForInsertion.IsCompleted)
					return new TimeoutResult<object>(null, true);
			}
		}

		object IQueueExit.Peek()
		{
			lock (_lock)
			{
				return _queues.FirstOrDefault(kv => kv.Value.Count != 0).Value?.Dequeue();
			}
		}
	}
}
