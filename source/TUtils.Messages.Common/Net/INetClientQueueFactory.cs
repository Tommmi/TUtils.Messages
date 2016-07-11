using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Common.Net
{
	public interface INetClientQueueFactory
	{
		IQueueTail CreateQueue(Uri serverAddress);
	}
}
