namespace TUtils.Messages.Common.Queue
{
	public interface IQueue
	{
		IQueueEntry Entry { get; }
		IQueueExit Exit { get; }
	}
}