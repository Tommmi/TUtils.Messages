using System.Threading;
using TUtils.Common.Common;
using TUtils.Common.Logging;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue
{
	public class ReliableQueueAdapterFactory : IQueueAdapterFactory
	{
		private readonly int _timeout;
		private readonly IQueueFactory _queueFactory;
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private readonly IReliableMessageProtocol _reliableMessageProtocol;
		private readonly CancellationToken _cancellationToken;

		public ReliableQueueAdapterFactory(
			IQueueFactory queueFactory,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			IReliableMessageProtocol reliableMessageProtocol,
			CancellationToken cancellationToken,
			int timeout)
		{
			_timeout = timeout;
			_queueFactory = queueFactory;
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_reliableMessageProtocol = reliableMessageProtocol;
			_cancellationToken = cancellationToken;
		}

		IQueueTail IQueueAdapterFactory.Create(
			IQueueEntry queueEntry,
			IQueueExit queueExit)
		{
			return new ReliableQueueAdapterBase(
				queueEntry,
				queueExit,
				_timeout,
				_queueFactory,
				_uniqueTimeStampCreator,
				_reliableMessageProtocol,
				_cancellationToken);
		}
	}
}