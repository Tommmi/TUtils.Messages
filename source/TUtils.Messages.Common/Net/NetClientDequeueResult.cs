namespace TUtils.Messages.Common.Net
{
	public class NetClientDequeueResult
	{
		public NetActionResultEnum Result { get; private set; }

		public MessageContent MessageContent { get; private set; }

		public NetClientDequeueResult(NetActionResultEnum result, MessageContent messageContent)
		{
			MessageContent = messageContent;
			Result = result;
		}
	}
}