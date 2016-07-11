namespace TUtils.Messages.Common.Net
{
	public class DequeueResponse
	{
		public ResponseEnum Result { get; }
		public MessageContent Content { get; }

		public DequeueResponse(ResponseEnum result, MessageContent content)
		{
			Result = result;
			Content = content;
		}
	}
}