using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Queue.Messages;

namespace TUtils.Messages.Core.Queue.Common
{
	public class QueueEntryProtocol : IQueueEntryProtocol
	{
		IEnqueueRequest IQueueEntryProtocol.CreateEnqueueRequest(long queueId, object message)
		{
			return new EnqueueRequest(queueId,message);
		}
	}
}