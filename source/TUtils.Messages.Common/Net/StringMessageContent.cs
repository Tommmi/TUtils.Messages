using TUtils.Common.Extensions;
// ReSharper disable MemberCanBePrivate.Global

namespace TUtils.Messages.Common.Net
{
	public class StringMessageContent : MessageContent
	{
		public string MessageContent { get; private set; }

		public StringMessageContent(string messageContent)
		{
			MessageContent = messageContent;
		}

		public override byte[] GetData()
		{
			return MessageContent.ToUTF8CodedByteArray();
		}
	}
}