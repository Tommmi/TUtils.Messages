using System;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.BusStop
{
	public abstract class BusAdapterBase : IMessageBusBase
	{
		#region types

		protected class QueueProxy : IQueueEntry
		{
			private readonly IQueueEntry _queueEntry;
			private readonly Func<object, Func<object, Task>, Task> _messageHook;

			public QueueProxy(
				IQueueEntry queueEntry,
				Func<object, Func<object, Task>, Task> messageHook)
			{
				_queueEntry = queueEntry;
				_messageHook = messageHook;
			}

			Task IQueueEntry.Enqueue(object msg)
			{
				return _messageHook(msg, ProceedWithMessage);
			}

			private Task ProceedWithMessage(object msg)
			{
				return _queueEntry.Enqueue(msg);
			}
		}

		#endregion

		#region fields

		/// <summary>
		/// (registrationId, in message bus registered handler or queue, origin handler or queue)
		/// </summary>
		// ReSharper disable once InconsistentNaming
		protected readonly IndexedTable<long, object, object> _registrations = new IndexedTable<long, object, object>();
		// ReSharper disable once InconsistentNaming
		protected readonly object _sync = new object();
		// ReSharper disable once InconsistentNaming
		protected readonly IMessageBusBase _messageBus;
		// ReSharper disable once InconsistentNaming
		protected readonly QueueProxy _queueToBusProxy;

		#endregion

		#region constructor

		protected BusAdapterBase(IMessageBusBase messageBus)
		{
			_messageBus = messageBus;
			_queueToBusProxy = new QueueProxy(messageBus.SendPort, OnMessageToBus);

		}

		#endregion

		#region abstract

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg"></param>
		/// <param name="proceed">
		/// Task Proceed(object message);
		/// call this method to send message to message bus 
		/// </param>
		protected abstract Task OnMessageToBus(object msg, Func<object, Task> proceed);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg"></param>
		/// <param name="proceed">
		/// Task Proceed(object message);
		/// call this delegate to handle message now
		/// </param>
		protected abstract Task OnMessageFromBus(object msg, Func<object, Task> proceed);

		#endregion

		#region IMessageBusBase

		IQueueEntry IMessageBusBase.SendPort => _queueToBusProxy;

		Task IMessageBusBase.Register(IAddress destinationAddress, IQueueEntry destinationQueue, long registrationId)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(registrationId, queueAdapter, destinationQueue));
			}
			return _messageBus.Register(destinationAddress, queueAdapter, registrationId);
		}

		Task IMessageBusBase.Register(Type messageType, IQueueEntry destinationQueue, long registrationId)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(registrationId, queueAdapter, destinationQueue));
			}
			return _messageBus.Register(messageType, queueAdapter, registrationId);
		}

		Task IMessageBusBase.RegisterBroadcast(IQueueEntry destinationQueue, long registrationId)
		{
			var queueAdapter = new QueueProxy(destinationQueue, OnMessageFromBus);
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, object, object>(registrationId, queueAdapter, destinationQueue));
			}
			return _messageBus.RegisterBroadcast(queueAdapter, registrationId);
		}

		Task IMessageBusBase.Unregister(long registrationId)
		{
			lock (_sync)
			{
				_registrations.RemoveAllMatchingItem1(registrationId);
			}
			return _messageBus.Unregister(registrationId);
		}

		Task IMessageBusBase.RegisterBridge(IBridge bridge)
		{
			return _messageBus.RegisterBridge(bridge);
		}

		Task IMessageBusBase.UnregisterBridge(IBridge bridge)
		{
			return _messageBus.UnregisterBridge(bridge);
		}

		Task<string> IMessageBusBase.GetBusName()
		{
			return _messageBus.GetBusName();
		}

		Task IMessageBusBase.WaitForIdle()
		{
			return _messageBus.WaitForIdle();
		}


		#endregion
	}
}
