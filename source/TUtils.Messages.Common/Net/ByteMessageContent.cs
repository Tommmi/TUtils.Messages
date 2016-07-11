namespace TUtils.Messages.Common.Net
{
	public class ByteMessageContent : MessageContent
	{
		public byte[] MessageContent { get; private set; }

		public ByteMessageContent(byte[] messageContent)
		{
			MessageContent = messageContent;
		}

		public override byte[] GetData()
		{
			return MessageContent;
		}
	}
}