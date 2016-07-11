namespace TUtils.Messages.Common.Queue.messages
{
	public interface IQueueEntryProtocol
	{
		IEnqueueRequest CreateEnqueueRequest(long queueId, object message);
	}
}