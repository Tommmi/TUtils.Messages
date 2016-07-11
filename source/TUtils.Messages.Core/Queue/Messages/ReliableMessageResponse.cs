using System;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	[Serializable]
	public class ReliableMessageResponse : IReliableMessageResponse
	{
		private readonly long _requestId;

		public ReliableMessageResponse(IReliableMessageRequest request)
		{
			_requestId = request.RequestId;
		}


		long IReliableMessageResponse.RequestId => _requestId;
	}
}