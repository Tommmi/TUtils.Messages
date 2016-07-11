using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TUtils.Messages.Common.Queue.messages
{
	public interface IReliableMessageRequest
	{
		object Message { get; }
		long RequestId { get; }
	}
}
