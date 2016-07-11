namespace TUtils.Messages.Common.Queue.messages
{
	public interface IReliableMessageProtocol
	{
		IReliableMessageResponse CreateReliableMessageResponse(IReliableMessageRequest request);
		IReliableMessageRequest CreateReliableMessageRequest(object message);
	}
}