namespace TUtils.Messages.Common.Queue.messages
{
	public interface IEnqueueRequest
	{
		long QueueId { get; }
		object Message { get; }
	}
}