using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Common;
using TUtils.Common.Logging;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Queue.Messages;

namespace TUtils.Messages.Core.Queue
{
	public class ReliableQueueAdapterBase : QueueAdapterBase
	{
		#region fields

		private readonly int _timeout;
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private readonly IReliableMessageProtocol _reliableMessageProtocol;

		private readonly object _sync = new object();
		private readonly int _firstTimeoutMs;
		private readonly long _id;
		#endregion

		#region constructor

		public ReliableQueueAdapterBase(
			IQueueEntry queueEntry,
			IQueueExit queueExit,
			int timeout,
			IQueueFactory queueFactory,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			IReliableMessageProtocol reliableMessageProtocol,
			CancellationToken cancellationToken) : base(queueFactory,queueEntry,queueExit,cancellationToken)
		{
			_timeout = timeout;
			_firstTimeoutMs = 100;
			_id = uniqueTimeStampCreator.Create();
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_reliableMessageProtocol = reliableMessageProtocol;
		}

		#endregion

		#region Member

		private int GetNextTimeout(int currentTimeout)
		{
			var timeout = currentTimeout*2;
			if (currentTimeout*4 > _timeout)
				timeout = _timeout - timeout;
			return timeout;
		}

		#endregion

		#region adapter overrides

		protected override Task DequeueHook(object msg)
		{
			var reliableMessageRequest = msg as IReliableMessageRequest;
			if (reliableMessageRequest != null)
			{
				ProceedEnqueue(_reliableMessageProtocol.CreateReliableMessageResponse(reliableMessageRequest));
				msg = reliableMessageRequest.Message;
			}
			return ProceedDequeue(msg);
		}

		protected override async Task EnqueueHook(object msg)
		{
			var timeout = _firstTimeoutMs;

			while (timeout > 0)
			{
				var requestId = _uniqueTimeStampCreator.Create();
				await ProceedEnqueue(new ReliableMessageRequest(requestId, msg));
				var result = await WaitOnReceivingMessage<IReliableMessageResponse>(timeoutMs:timeout,filter: m => m.RequestId == requestId);
				if (!result.TimeoutElapsed)
					return;
				timeout = GetNextTimeout(timeout);
			}
		}

		#endregion
	}
}
