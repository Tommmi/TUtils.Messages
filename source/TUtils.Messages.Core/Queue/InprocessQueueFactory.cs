using System.Collections.Generic;
using System.Threading;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.Queue
{
	public class InprocessQueueFactory : IQueueFactory
	{
		private readonly CancellationToken _cancellationToken;

		private readonly List<InProcessQueue.InProcessQueue> _queuePool = new List<InProcessQueue.InProcessQueue>();
		private readonly object _lock = new object();

		public InprocessQueueFactory(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
		}

		public void ReuseQueue(IQueue queue)
		{
			var item = queue as InProcessQueue.InProcessQueue;
			if (item != null)
			{
				item.Clear();

				lock (_lock)
				{
					_queuePool.Add(item);
				}
			}
		}

		public IQueue Create()
		{
			lock (_lock)
			{
				var lastIdx = _queuePool.Count - 1;
				if (lastIdx >= 0)
				{
					var queue = _queuePool[lastIdx];
					_queuePool.RemoveAt(lastIdx);
					return queue;
				}
			}

			return new InProcessQueue.InProcessQueue(_cancellationToken);
		}
	}
}