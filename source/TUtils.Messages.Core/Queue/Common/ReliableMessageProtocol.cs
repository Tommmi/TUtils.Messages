using TUtils.Common.Common;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	public class ReliableMessageProtocol : IReliableMessageProtocol
	{
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;

		public ReliableMessageProtocol(
			IUniqueTimeStampCreator uniqueTimeStampCreator)
		{
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
		}

		IReliableMessageResponse IReliableMessageProtocol.CreateReliableMessageResponse(IReliableMessageRequest request)
		{
			return new ReliableMessageResponse(request);
		}

		IReliableMessageRequest IReliableMessageProtocol.CreateReliableMessageRequest(object message)
		{
			return new ReliableMessageRequest(_uniqueTimeStampCreator.Create(),message);
		}
	}
}