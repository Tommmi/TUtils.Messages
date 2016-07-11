using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TUtils.Messages.Common.Queue
{
	public interface IQueueAdapterFactory
	{
		IQueueTail Create(
			IQueueEntry queueEntry,
			IQueueExit queueExit);
	}
}
