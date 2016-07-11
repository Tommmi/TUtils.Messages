namespace TUtils.Messages.Common.Net
{
	public class EnqueueResponse
	{
		public ResponseEnum Result { get; }

		public EnqueueResponse(ResponseEnum result)
		{
			Result = result;
		}
	}
}