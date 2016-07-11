using System;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	[Serializable]
	public class ReliableMessageRequest : IReliableMessageRequest
	{
		private readonly long _requestId;
		private readonly object _message;

		public ReliableMessageRequest(long requestId, object message)
		{
			_requestId = requestId;
			_message = message;
		}

		object IReliableMessageRequest.Message => _message;

		long IReliableMessageRequest.RequestId => _requestId;
	}
}
