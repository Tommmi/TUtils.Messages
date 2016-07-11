using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.BusStop
{
	public abstract class BusAdapter : BusAdapterBase, IMessageBus
	{
		#region types

		private class MessageHandlerProxy<TMessageType>
		{
			private readonly Func<TMessageType, Task> _asyncMessageHandler;
			private readonly Func<TMessageType, Func<TMessageType, Task>, Task> _messageHook;

			public Func<TMessageType, Task> Handler { get; }


			public MessageHandlerProxy(
				Func<TMessageType, Task> asyncMessageHandler,
				Func<TMessageType, Func<TMessageType, Task>, Task> messageHook)
			{
				_asyncMessageHandler = asyncMessageHandler;
				_messageHook = messageHook;
				Handler = HandlerInternal;

			}

			private Task HandlerInternal(TMessageType addressedMessage)
			{
				return _messageHook(addressedMessage, ProceedWithMessage);
			}

			private Task ProceedWithMessage(TMessageType messageType)
			{
				return _asyncMessageHandler(messageType);
			}
		}

		#endregion

		#region constructor

		protected BusAdapter(IMessageBus messageBus) : base(messageBus)
		{
		}

		#endregion

		#region IMessageBus


		Task IMessageBus.Register(IAddress destinationAddress, Func<IAddressedMessage, Task> asyncMessageHandler)
		{
			var handlerProxy = new MessageHandlerProxy<IAddressedMessage>(
				asyncMessageHandler,
				(msg,proceed)=>OnMessageFromBus(msg,m=>proceed(m as IAddressedMessage)));

			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0,handlerProxy.Handler,asyncMessageHandler));
			}
			return ((IMessageBus) _messageBus).Register(destinationAddress, handlerProxy.Handler);
		}

		Task IMessageBus.Register(IAddress destinationAddress, IQueueEntry destinationQueue)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0, queueAdapter, destinationQueue));
			}
			return ((IMessageBus) _messageBus).Register(destinationAddress, queueAdapter);
		}

		Task IMessageBus.Register(Type messageType, Func<object, Task> asyncMessageHandler)
		{
			var handlerProxy = new MessageHandlerProxy<object>(asyncMessageHandler, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0, handlerProxy.Handler, asyncMessageHandler));
			}
			return ((IMessageBus) _messageBus).Register(messageType, handlerProxy.Handler);
		}

		Task IMessageBus.Register(Type messageType, IQueueEntry destinationQueue)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0, queueAdapter, destinationQueue));
			}
			return ((IMessageBus) _messageBus).Register(messageType, queueAdapter);
		}

		Task IMessageBus.Register<TMessageType>(Func<object, Task> asyncMessageHandler)
		{
			return ((IMessageBus) this).Register(typeof (TMessageType), asyncMessageHandler);
		}

		Task IMessageBus.Register<TMessageType>(IQueueEntry destinationQueue)
		{
			return ((IMessageBus)this).Register(typeof(TMessageType), destinationQueue);
		}

		Task IMessageBus.RegisterBroadcast(IQueueEntry destinationQueue)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0, queueAdapter, destinationQueue));
			}
			return ((IMessageBus) _messageBus).RegisterBroadcast(queueAdapter);
		}

		Task IMessageBus.RegisterBroadcast(Func<object, Task> asyncMessageHandler)
		{
			var handlerProxy = new MessageHandlerProxy<object>(asyncMessageHandler, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(0, handlerProxy.Handler, asyncMessageHandler));
			}
			return ((IMessageBus) _messageBus).RegisterBroadcast(handlerProxy.Handler);
		}

		async Task IMessageBus.Unregister(Func<object, Task> asyncMessageHandler)
		{
			IEnumerable<Func<object, Task>> registeredHandlers;
			lock (_sync)
			{
				registeredHandlers = _registrations.FindByItem3(asyncMessageHandler).Select(t=>t.Item2 as Func<object, Task>);
				_registrations.RemoveAllMatchingItem3(asyncMessageHandler);
			}
			foreach (var registeredHandler in registeredHandlers)
			{
				await ((IMessageBus) _messageBus).Unregister(registeredHandler);
			}
		}

		async Task IMessageBus.Unregister(Func<IAddressedMessage, Task> asyncMessageHandler)
		{
			IEnumerable<Func<IAddressedMessage, Task>> registeredHandlers;
			lock (_sync)
			{
				registeredHandlers = _registrations.FindByItem3(asyncMessageHandler).Select(t => t.Item2 as Func<IAddressedMessage, Task>);
				_registrations.RemoveAllMatchingItem3(asyncMessageHandler);
			}
			foreach (var registeredHandler in registeredHandlers)
			{
				await ((IMessageBus) _messageBus).Unregister(registeredHandler);
			}
		}

		async Task IMessageBus.Unregister(IQueueEntry destinationQueue)
		{
			IEnumerable<IQueueEntry> registeredQueues;
			lock (_sync)
			{
				registeredQueues = _registrations.FindByItem3(destinationQueue).Select(t => t.Item2 as IQueueEntry);
				_registrations.RemoveAllMatchingItem3(destinationQueue);
			}
			foreach (var registeredQueue in registeredQueues)
			{
				await ((IMessageBus) _messageBus).Unregister(registeredQueue);
			}
		}

		#endregion
	}
}