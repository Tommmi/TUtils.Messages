namespace TUtils.Messages.Common.Queue
{
	public interface IQueueFactory
	{
		IQueue Create();
		void ReuseQueue(IQueue queue);
	}
}