using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	[Serializable]
	public class EnqueueRequest : IEnqueueRequest
	{
		public EnqueueRequest(long queueId, object message)
		{
			QueueId = queueId;
			Message = message;
		}

		public long QueueId { get; }
		public object Message { get; }
	}
}
