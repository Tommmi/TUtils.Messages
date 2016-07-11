namespace TUtils.Messages.Common.Net
{
	public enum StreamReadResultEnum
	{
		OnRunning,
		TerminatedWithSuccess,
		CanceledByTimeout,
		CanceledByClient,
		CanceledByServer,
		NotAByteStream,
		NotAStringStream,
		UnknownError
	}
}