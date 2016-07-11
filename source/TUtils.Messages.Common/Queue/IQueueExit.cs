using System.Threading.Tasks;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Queue
{
	public interface IQueueExit
	{
		Task<object> Dequeue();
		Task<TimeoutResult<object>> Dequeue(int timeoutMs);
		object Peek();
	}
}