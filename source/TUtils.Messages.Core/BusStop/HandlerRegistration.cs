using System;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;

namespace TUtils.Messages.Core.BusStop
{
	public class HandlerRegistration<TMessageType> : IHandlerRegistration
	{
		private readonly Func<TMessageType, bool> _filter;
		private readonly IMessageBus _bus;
		private readonly Func<TMessageType, CancellationToken, Task> _handler;
		private readonly BusStop _busStop;
		private readonly CancellationToken _cancellationToken;
		private readonly bool _includingBroadcastMessages;

		public HandlerRegistration(
			Func<TMessageType, bool> filter, 
			IMessageBus bus, 
			Func<TMessageType, CancellationToken, Task> handler, 
			BusStop busStop,
			CancellationToken cancellationToken,
			bool includingBroadcastMessages)
		{
			_filter = filter;
			_bus = bus;
			_handler = handler;
			_busStop = busStop;
			_cancellationToken = cancellationToken;
			_includingBroadcastMessages = includingBroadcastMessages;
		}

		public async Task OnMessage(object o)
		{
			if (o is TMessageType && _filter((TMessageType)o))
				await _handler((TMessageType)o, _cancellationToken);
		}

		public void Unregister()
		{
			if ( _includingBroadcastMessages)
				_bus.Unregister(OnMessage);
			else
				_busStop.UnregisterHandlerInternal(OnMessage);
		}
	}
}