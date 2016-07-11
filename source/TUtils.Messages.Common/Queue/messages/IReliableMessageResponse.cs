namespace TUtils.Messages.Common.Queue.messages
{
	public interface IReliableMessageResponse
	{
		long RequestId { get; }
	}
}