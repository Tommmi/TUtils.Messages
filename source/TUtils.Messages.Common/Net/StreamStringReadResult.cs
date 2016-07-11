namespace TUtils.Messages.Common.Net
{
	public class StreamStringReadResult
	{
		public StreamReadResultEnum Result { get; set; }
		public string ReadData { get; set; }

		public StreamStringReadResult(StreamReadResultEnum result, string readData)
		{
			Result = result;
			ReadData = readData;
		}
	}
}