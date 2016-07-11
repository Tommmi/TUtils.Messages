using System.Threading.Tasks;

namespace TUtils.Messages.Common.Queue
{
    public interface IQueueEntry
    {
	    Task Enqueue(object msg);
    }
}
