using System;
using System.Threading;
using System.Threading.Tasks;

namespace TUtils.Messages.Common.BusStop
{
	public interface IBusStopOn<TMessageType>
	{
		IHandlerRegistration Do(Func<TMessageType, CancellationToken, Task> handler);
		IBusStopOn<TMessageType> IncludingMessagesToOtherBusStops();
		IBusStopOn<TMessageType> FilteredBy(Func<TMessageType, bool> filter);
	}
}