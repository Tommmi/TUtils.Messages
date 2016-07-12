using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Async;
using TUtils.Common.Extensions;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core
{
	/// <summary>
	/// A message bus holds a message queue and dispatches these messages 
	/// to all handlers, which have registered for messages.
	/// The registration includes a filter which messages the handler wants to consume.
	/// The filter contains
	/// - destination address
	/// - type of message
	/// A message bus contains a message loop, running on the task scheduler.
	/// A message bus knows how many handlers are still busy and uses this piece information to 
	/// implement the method IMessageBus.WaitForIdle(). See also parameter "maxCountRunningTasks" of constructor.
	/// </summary>
	public class MessageBus : IMessageBus
	{
		#region types

		/// <summary>
		/// A comparer which compares two message registrations:
		/// Two registrations are equal, if they have the same destination.
		/// For example: if a bus client has registered it's input queue for 
		/// message of type "MyMessage" a n d  has registered it's input queue for 
		/// messages which are addressed to him, these two registrations are equal
		/// because they have the same message aim: it's input queue.
		/// The tuple is the registration data.
		/// </summary>
		private class SameHandlerComparer : EqualityComparer<Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>>
		{
			public override bool Equals(
				Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long> x, 
				Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long> y)
			{
				return
					x.Item4 == y.Item4
					&& x.Item5 == y.Item5
					&& x.Item6 == y.Item6;
			}

			public override int GetHashCode(Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long> obj)
			{
				int hashCode = 0;

				hashCode = obj?.Item4?.GetHashCode() ?? 0 + (hashCode << 6) + (hashCode << 16) - hashCode;
				hashCode = obj?.Item5?.GetHashCode() ?? 0 + (hashCode << 6) + (hashCode << 16) - hashCode;
				hashCode = obj?.Item6?.GetHashCode() ?? 0 + (hashCode << 6) + (hashCode << 16) - hashCode;

				return hashCode;
			}
		}

		private class Address : IEquatable<Address>
		{
			public IAddress AddressObject { get; }

			public Address(IAddress address)
			{
				AddressObject = address;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(obj,null))
					return false;
				var address = obj as Address;
				if (address != null)
				{
					return AddressObject.IsEqual(address.AddressObject);
				}

				return false;
			}

			public override int GetHashCode()
			{
				return AddressObject.Hash;
			}

			public bool Equals(Address other)
			{
				return AddressObject.IsEqual(other.AddressObject);
			}

			public override string ToString()
			{
				return AddressObject.ToString();
			}
		}

		#endregion

		#region fields

		private readonly CancellationToken _cancellationToken;
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private readonly int _maxCountRunningTasks;
		private readonly ITLog _logger;
		private readonly IQueue _inputQueue;
		private readonly AsyncEvent _evIsIdle;

		private readonly IndexedTable<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long> 
			_routingTable = new IndexedTable<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>();
		private readonly List<IBridge> _bridges = new List<IBridge>();
		private readonly Task _runTask;


		private readonly object _lock = new object();
		public bool IsRunning => !(_runTask.IsCompleted || _runTask.IsFaulted || _runTask.IsCanceled);
		private readonly SameHandlerComparer _sameHandlerComparer;

		private List<Task> _runningTasks;
		private string _busName;

		#endregion

		#region constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="busName">
		/// for logging purpose
		/// </param>
		/// <param name="cancellationToken"></param>
		/// <param name="queueFactory">
		/// Used to generate the input queue of this bus. All bus stops of this bus puts their messages into this input queue.
		/// e.g.: <code>var queueFactory = new InprocessQueueFactory(cancellationToken);</code>
		/// </param>
		/// <param name="uniqueTimeStampCreator">
		/// e.g.: <code>var timeStampCreator = new UniqueTimeStampCreator();</code>
		/// </param>
		/// <param name="maxCountRunningTasks">
		/// maximum wished count of messages, which are being processed.
		/// This value has effect to method WaitForIdle() only !
		/// </param>
		/// <param name="logger"></param>
		public MessageBus(
			string busName,
			IQueueFactory queueFactory, 
			CancellationToken cancellationToken,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			int maxCountRunningTasks,
			ITLog logger)
		{
			_cancellationToken = cancellationToken;
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_maxCountRunningTasks = maxCountRunningTasks;
			_logger = logger;
			_runningTasks = new List<Task>();
			_busName = busName;
			_inputQueue = queueFactory.Create();
			_evIsIdle = new AsyncEvent(cancellationToken);
			_sameHandlerComparer = new SameHandlerComparer();
			_runTask = Task.Run(()=>Run().LogExceptions(logger),cancellationToken);
		}

		#endregion

		#region Member

		private async Task Run()
		{
			while (true)
			{
				_cancellationToken.ThrowIfCancellationRequested();
				var msg = await _inputQueue.Exit.Dequeue();
				var address = (msg as IAddressedMessage)?.Destination;

				LogMessage(msg);

				IEnumerable<Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>>
					registeredhandlers = new List<Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>>();

				lock (_lock)
				{
					_runningTasks.RemoveWhere(task => task.IsCompleted || task.IsCanceled || task.IsFaulted);
					if (_runningTasks.Count < _maxCountRunningTasks)
						_evIsIdle.Rise();

					if (address != null)
						registeredhandlers = _routingTable.FindByItem1(new Address(address));

					registeredhandlers = registeredhandlers
						.Concat(_routingTable.FindByItem2(msg.GetType()))
						.Concat(_routingTable.FindByItem3(true));
				}

				// Remove registrations with the same destination.
				// Avoid sending a message twice to the same handler.
				registeredhandlers = registeredhandlers.Distinct(_sameHandlerComparer);

				foreach (var registeredhandler in registeredhandlers)
				{
					var queue = registeredhandler.Item4;
					if (queue != null)
						StoreRunningTask(queue.Enqueue(msg));

					var handler = registeredhandler.Item5;
					if (handler != null)
						StoreRunningTask(Task.Run(() => handler(msg).LogExceptions(_logger), _cancellationToken));

					var handler2 = registeredhandler.Item6;
					if (handler2 != null)
						StoreRunningTask(Task.Run(() => handler2(msg as IAddressedMessage).LogExceptions(_logger), _cancellationToken));
				}
			}
			// ReSharper disable once FunctionNeverReturns
		}

		private void LogMessage(object msg)
		{
			if (_logger.IsActive(LogSeverityEnum.INFO, this))
			{
				string loggingText = $"bus {_busName}: message {msg.GetType().Name}";

				var message = msg as IAddressedMessage;
				if (message != null)
				{
					loggingText += $" source:{message.Source}, destination:{message.Destination}";
				}

				loggingText += $" content:{msg}";
				_logger.LogInfo(this, loggingText);
			}
		}

		private void StoreRunningTask(Task task)
		{
			lock (_lock)
			{
				_runningTasks.Add(task);
			}
		}

		private void DoForAllBridges(Action<IBridge> doForBridge)
		{
			IEnumerable<IBridge> bridges;
			lock (_lock)
			{
				bridges = _bridges.ToList();
			}

			foreach (var bridge in bridges)
			{
				doForBridge(bridge);
			}
		}

		private long CreateRegistrationId()
		{
			return _uniqueTimeStampCreator.Create();
		}

		private void Unregister(Func<IEnumerable<long>> getRegistrationIds)
		{
			IEnumerable<long> registrationIds;
			IEnumerable<IBridge> bridges;

			lock (_lock)
			{
				registrationIds = getRegistrationIds();
				bridges = _bridges.ToList();
			}

			foreach (var registrationId in registrationIds)
			{
				foreach (var bridge in bridges)
					bridge.OnUnregister(registrationId);
			}
		}

		#endregion

		#region IMessageBus

#pragma warning disable 1998
		async Task<string> IMessageBusBase.GetBusName()
#pragma warning restore 1998
		{
			return _busName;
		}

		/// <summary>
		/// see IMessageBusBase.WaitForIdle for further information
		/// </summary>
		/// <returns></returns>
		async Task IMessageBusBase.WaitForIdle()
		{
			Task waitForIdle;
			lock (_lock)
			{
				if (_runningTasks.Count < _maxCountRunningTasks)
					return;
				waitForIdle = _evIsIdle.RegisterForEvent();
			}

			await waitForIdle;
		}

		IQueueEntry IMessageBusBase.SendPort => _inputQueue.Entry;

		/// <summary>
		/// see IMessageBusBase.Register for further information
		/// </summary>
		/// <returns></returns>
		Task IMessageBus.Register(IAddress destinationAddress, Func<IAddressedMessage, Task> asyncMessageHandler)
		{
			long registrationId = CreateRegistrationId();
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					new Address(destinationAddress),
					null,
					false,
					null,
					null,
					asyncMessageHandler,
					registrationId));
			}

			DoForAllBridges(bridge=>bridge.OnRegister(destinationAddress, registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBus.Register(IAddress destinationAddress, IQueueEntry destinationQueue)
		{
			(this as IMessageBus).Register(destinationAddress, destinationQueue, CreateRegistrationId());
			return Task.CompletedTask;
		}

		Task IMessageBusBase.Register(IAddress destinationAddress, IQueueEntry destinationQueue, long registrationId)
		{
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					new Address(destinationAddress),
					null,
					false,
					destinationQueue,
					null,
					null,
					registrationId));
			}

			DoForAllBridges(bridge => bridge.OnRegister(destinationAddress, registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBus.Register(Type messageType, Func<object, Task> asyncMessageHandler)
		{
			long registrationId = CreateRegistrationId();
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					messageType,
					false,
					null,
					asyncMessageHandler,
					null,
					registrationId));
			}

			DoForAllBridges(bridge => bridge.OnRegister(messageType, registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBus.Register(Type messageType, IQueueEntry destinationQueue)
		{
			long registrationId = CreateRegistrationId();
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					messageType,
					false,
					destinationQueue,
					null,
					null,
					registrationId));
			}
			DoForAllBridges(bridge => bridge.OnRegister(messageType, registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBusBase.Register(Type messageType, IQueueEntry destinationQueue, long registrationId)
		{
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					messageType,
					false,
					destinationQueue,
					null,
					null,
					registrationId));
			}
			DoForAllBridges(bridge => bridge.OnRegister(messageType, registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBus.Register<TMessageType>(Func<object, Task> asyncMessageHandler)
		{
			lock (_lock)
			{
				(this as IMessageBus).Register(typeof(TMessageType), asyncMessageHandler);
			}
			return Task.CompletedTask;
		}

		Task IMessageBus.Register<TMessageType>(IQueueEntry destinationQueue)
		{
			lock (_lock)
			{
				(this as IMessageBus).Register(typeof(TMessageType), destinationQueue);
			}
			return Task.CompletedTask;
		}

		Task IMessageBus.RegisterBroadcast(IQueueEntry destinationQueue)
		{
			long registrationId = CreateRegistrationId();
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					null,
					true,
					destinationQueue,
					null,
					null,
					registrationId));
			}
			DoForAllBridges(bridge => bridge.OnRegisterBroadcast(registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBusBase.RegisterBroadcast(IQueueEntry destinationQueue, long registrationId)
		{
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					null,
					true,
					destinationQueue,
					null,
					null,
					registrationId));
			}
			DoForAllBridges(bridge => bridge.OnRegisterBroadcast(registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBus.RegisterBroadcast(Func<object, Task> asyncMessageHandler)
		{
			long registrationId = CreateRegistrationId();
			lock (_lock)
			{
				_routingTable.Insert(new Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>(
					null,
					null,
					true,
					null,
					asyncMessageHandler,
					null,
					registrationId));
			}
			DoForAllBridges(bridge => bridge.OnRegisterBroadcast(registrationId));
			return Task.CompletedTask;
		}

		Task IMessageBusBase.RegisterBridge(IBridge bridge)
		{
			IEnumerable<Tuple<Address, Type, bool, IQueueEntry, Func<object, Task>, Func<IAddressedMessage, Task>, long>>
				allRegisteredHandlers;

			lock (_lock)
			{
				if (_bridges.Contains(bridge))
					return Task.CompletedTask;
				_bridges.Add(bridge);
				allRegisteredHandlers = _routingTable.GetAllRows();
			}

			foreach (var handlerRegistration in allRegisteredHandlers)
			{
				var registrationId = handlerRegistration.Item7;
				var address = handlerRegistration.Item1;

				if (address != null)
				{
					bridge.OnRegister(address.AddressObject, registrationId);
					continue;
				}

				var type = handlerRegistration.Item2;
				if (type != null)
				{
					bridge.OnRegister(type, registrationId);
					continue;
				}

				bool isBroadcast = handlerRegistration.Item3;

				if (isBroadcast)
				{
					bridge.OnRegisterBroadcast(registrationId);
					continue;
				}

				throw new ApplicationException("78623878942u02");
			}
			return Task.CompletedTask;
		}

		Task IMessageBusBase.UnregisterBridge(IBridge bridge)
		{
			lock (_lock)
			{
				if (_bridges.Contains(bridge))
					return Task.CompletedTask;
				_bridges.Remove(bridge);
			}
			return Task.CompletedTask;
		}

		Task IMessageBus.Unregister(IQueueEntry destinationQueue)
		{
			Unregister(() =>
			{
				var registrationIds = _routingTable.FindByItem4(destinationQueue).Select(val => val.Item7).Distinct().ToList();
				_routingTable.RemoveAllMatchingItem4(destinationQueue);
				return registrationIds;
			});
			return Task.CompletedTask;
		}

		Task IMessageBus.Unregister(Func<object, Task> asyncMessageHandler)
		{
			Unregister(() =>
			{
				var registrationIds = _routingTable.FindByItem5(asyncMessageHandler).Select(val => val.Item7).Distinct().ToList();
				_routingTable.RemoveAllMatchingItem5(asyncMessageHandler);
				return registrationIds;
			});
			return Task.CompletedTask;
		}

		Task IMessageBus.Unregister(Func<IAddressedMessage, Task> asyncMessageHandler)
		{
			Unregister(() =>
			{
				var registrationIds = _routingTable.FindByItem6(asyncMessageHandler).Select(val => val.Item7).Distinct().ToList();
				_routingTable.RemoveAllMatchingItem6(asyncMessageHandler);
				return registrationIds;
			});
			return Task.CompletedTask;
		}

		Task IMessageBusBase.Unregister(long registrationId)
		{
			lock (_lock)
			{
				_routingTable.RemoveAllMatchingItem7(registrationId);
			}

			DoForAllBridges(bridge=> bridge.OnUnregister(registrationId));
			return Task.CompletedTask;
		}

		#endregion
	}
}