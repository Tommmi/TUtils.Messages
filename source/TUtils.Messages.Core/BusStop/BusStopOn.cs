using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;

namespace TUtils.Messages.Core.BusStop
{
	public class BusStopOn<TMessageType> : IBusStopOn<TMessageType>
	{
		private readonly List<Func<TMessageType, bool>> _filters = new List<Func<TMessageType, bool>>();
		private readonly IMessageBus _bus;
		private readonly BusStop _busStop;
		private readonly CancellationToken _cancellationToken;
		private bool _includingBroadcastMessages;

		public BusStopOn(IMessageBus bus, BusStop busStop, CancellationToken cancellationToken)
		{
			_bus = bus;
			_busStop = busStop;
			_cancellationToken = cancellationToken;
		}

		IHandlerRegistration IBusStopOn<TMessageType>.Do(Func<TMessageType, CancellationToken, Task> handler)
		{
			var registration = new HandlerRegistration<TMessageType>(Filter, _bus, handler, _busStop, _cancellationToken, _includingBroadcastMessages);

			if (_includingBroadcastMessages)
				_bus.Register<TMessageType>(registration.OnMessage);
			else
				_busStop.RegisterHandlerInternal(registration.OnMessage);
			
			return registration;
		}

		private bool Filter(TMessageType message)
		{
			return _filters.All(filter => filter(message));
		}

		IBusStopOn<TMessageType> IBusStopOn<TMessageType>.IncludingBroadcastMessages()
		{
			_includingBroadcastMessages = true;
			return this;
		}

		IBusStopOn<TMessageType> IBusStopOn<TMessageType>.FilteredBy(Func<TMessageType, bool> filter)
		{
			_filters.Add(filter);
			return this;
		}
	}
}