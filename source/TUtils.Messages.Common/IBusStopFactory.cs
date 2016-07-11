using System.Threading.Tasks;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common
{
	public interface IBusStopFactory
	{
		Task<IBusStop> Create(string nodeName);
		Task<IBusStop> Create(IAddress address);
	}
}