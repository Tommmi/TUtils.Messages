namespace TUtils.Messages.Common.Net
{
	public class StreamByteReadResult
	{
		public StreamReadResultEnum Result { get; set; }
		public byte[] ReadData { get; set; }

		public StreamByteReadResult(StreamReadResultEnum result, byte[] readData)
		{
			Result = result;
			ReadData = readData;
		}
	}
}