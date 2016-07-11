using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Extensions;
using TUtils.Common.Logging;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core
{
	/// <summary>
	/// A bridge joins two or more busses of type IMessagebus together, so that these busses act like 
	/// one single bus. Each bus stop registration at one bus will lead to a registration on all other joined busses.
	/// Messages occuring on one bus will be sent to other joined busses,  i f  there is a bus stop which 
	/// has been registered for that message.
	/// </summary>
    public class Bridge
    {
		// ReSharper disable once NotAccessedField.Local
		private readonly ITLog _logger;

		#region types

		private class DummyMessageBus : IMessageBusBase
		{
			IQueueEntry IMessageBusBase.SendPort
			{
				get { return null; }
			}

			Task IMessageBusBase.Register(IAddress destinationAddress, IQueueEntry destinationQueue, long registrationId)
			{
				return Task.CompletedTask;
			}

			Task IMessageBusBase.Register(Type messageType, IQueueEntry destinationQueue, long registrationId)
			{
				return Task.CompletedTask;
			}

			Task IMessageBusBase.RegisterBroadcast(IQueueEntry destinationQueue, long registrationId)
			{
				return Task.CompletedTask;
			}

			Task IMessageBusBase.Unregister(long registrationId)
			{
				return Task.CompletedTask;
			}

			Task IMessageBusBase.RegisterBridge(IBridge bridge)
			{
				return Task.CompletedTask;
			}

			Task IMessageBusBase.UnregisterBridge(IBridge bridge)
			{
				return Task.CompletedTask;
			}

#pragma warning disable 1998
			async Task<string> IMessageBusBase.GetBusName()
#pragma warning restore 1998
			{
				return "dummy bus";
			}

			Task IMessageBusBase.WaitForIdle()
			{
				return Task.CompletedTask;
			}
		}

		private class BusListener : IBridge
		{
			#region private

			#region fields

			private volatile bool _deactivated;
			private readonly IEnumerable<BusListener> _busListeners;
			/// <summary>
			/// reference to bridge lock
			/// </summary>
			private readonly object _sync;
			/// <summary>
			/// reference to Bridge._registrations
			/// Item1: registration ID
			/// Item2: source-messagebus which has requested the registration
			/// Item3: destination-messagebus on which the source-bus has registered for messages
			/// Item4: isBroadcastRegistration
			/// Item5: if != null: registration for messages of this type only
			/// Item6: if != null: registration for messages for this bus-stop-address only
			/// </summary>
			private readonly IndexedTable<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress> _registrations;

			#endregion

			private void OnRegister(long registrationId, Action<BusListener> onRegisterAtOtherBus)
			{
				if ( _deactivated)
					return;
				IEnumerable<BusListener> busListeners;
				lock (_sync)
				{
					if (_registrations.FindByItem1(registrationId).Any())
						return;
					busListeners = _busListeners.ToList();
				}

				foreach (var busListener in busListeners)
				{
					if (busListener != this)
					{
						onRegisterAtOtherBus(busListener);
					}
				}
			}

			#endregion

			#region constructor

			/// <summary>
			/// A listener, which listens to registration requests from a bus.
			/// So whenever a bus stop registers for messages on a bus, the associated bus listener in a bridge
			/// is called also.
			/// </summary>
			/// <param name="messageBus">the bus on which this listener listens to registration requests</param>
			/// <param name="busListeners">
			/// all bus listeners in the bridge
			/// </param>
			/// <param name="sync"></param>
			/// <param name="registrations"></param>
			public BusListener(
				IMessageBusBase messageBus, 
				IEnumerable<BusListener> busListeners, 
				object sync,
				IndexedTable<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress> registrations)
			{
				_busListeners = busListeners;
				_sync = sync;
				_registrations = registrations;
				MessageBus = messageBus;
			}

			#endregion

			#region public

			public IMessageBusBase MessageBus { get; }

			public void Deactivate()
			{
				if (_deactivated)
					return;
				_deactivated = true;
				MessageBus.UnregisterBridge(this);

				IEnumerable<Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>> registrationsOfThisBus;


				lock (_sync)
				{
					registrationsOfThisBus =
						_registrations.FindByItem2(MessageBus)
							.Concat(_registrations.FindByItem3(MessageBus))
							.ToList();

					_registrations.RemoveAllMatchingItem2(MessageBus);
					_registrations.RemoveAllMatchingItem3(MessageBus);
				}

				foreach (var registration in registrationsOfThisBus)
				{
					var destinationBus = registration.Item3;
					var registrationId = registration.Item1;
					destinationBus.Unregister(registrationId);
				}
			}


			#region IBridge

			void IBridge.OnRegister(IAddress destinationAddress, long registrationId)
			{
				if (_deactivated)
					return;
				OnRegister(registrationId, busListener =>
				{
					busListener.MessageBus.Register(destinationAddress, MessageBus.SendPort, registrationId);
					lock (_sync)
					{
						_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
							registrationId, 
							MessageBus, 
							busListener.MessageBus,
							false,
							null,
							destinationAddress));
					}
				});
			}

			void IBridge.OnRegister(Type messageType, long registrationId)
			{
				if (_deactivated)
					return;
				OnRegister(registrationId, busListener =>
				{
					busListener.MessageBus.Register(messageType, MessageBus.SendPort, registrationId);
					lock (_sync)
					{
						_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
							registrationId, 
							MessageBus,
							busListener.MessageBus,
							false,
							messageType,
							null));
					}
				});
			}

			void IBridge.OnRegisterBroadcast(long registrationId)
			{
				if (_deactivated)
					return;
				OnRegister(registrationId, busListener =>
				{
					busListener.MessageBus.RegisterBroadcast(MessageBus.SendPort, registrationId);
					lock (_sync)
					{
						_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
							registrationId, 
							MessageBus, 
							busListener.MessageBus,
							true,
							null,
							null));
					}
				});
			}

			void IBridge.OnUnregister(long registrationId)
			{
				if (_deactivated)
					return;
				IEnumerable<Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>> registrationsOfThisId;
				lock (_sync)
				{
					registrationsOfThisId = _registrations.FindByItem1(registrationId);
					_registrations.RemoveAllMatchingItem1(registrationId);
				}
				foreach (var registration in registrationsOfThisId)
				{
					var destinationBus = registration.Item3;
					destinationBus.Unregister(registrationId);
				}
			}

			#endregion

			#endregion
		}

		#endregion

		#region fields

		private volatile bool _deactivated;
		private readonly object _sync = new object();
		/// <summary>
		/// dummy bus for collecting registartions
		/// </summary>
		private readonly IMessageBusBase _voidBus;
		/// <summary>
		/// Item1: registration ID
		/// Item2: source message bus which has requested the registration
		/// Item3: destination message bus on which the source bus has registered for messages
		/// </summary>
		private readonly IndexedTable<long,IMessageBusBase, IMessageBusBase, bool, Type, IAddress> _registrations = new IndexedTable<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>();
		private readonly List<BusListener> _busListeners = new List<BusListener>();

		#endregion

		#region constructor

		public Bridge(ITLog logger)
		{
			_logger = logger;
			_voidBus = new DummyMessageBus();
#pragma warning disable 4014
			AddBus(_voidBus).LogExceptions(logger);
#pragma warning restore 4014
		}

		#endregion

		#region methods

		public async Task AddBus(IMessageBusBase messageBus)
		{
			if (_deactivated)
				return;

			BusListener busListener;
		    IEnumerable<Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>> existingRegistrations;

			lock (_sync)
			{
				if (_busListeners.Any(bus=>bus.MessageBus == messageBus))
					return;
				existingRegistrations = _registrations.FindByItem3(_voidBus);
				busListener = new BusListener(messageBus, _busListeners, _sync, _registrations);
				_busListeners.Add(busListener);
			}

			await messageBus.RegisterBridge(busListener);

		    foreach (var existingRegistration in existingRegistrations)
		    {
			    var registrationId = existingRegistration.Item1;
			    var sourceMessagebus = existingRegistration.Item2;
			    var messageType = existingRegistration.Item5;
			    var isBroadcastRegistration = existingRegistration.Item4;
			    var destinationAddress = existingRegistration.Item6;

			    if (destinationAddress != null)
			    {
					_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
						registrationId,
						sourceMessagebus,
						messageBus,
						false,
						null,
						destinationAddress));
					await messageBus.Register(destinationAddress,sourceMessagebus.SendPort,registrationId);
			    }
				else if (messageType != null)
				{
					_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
						registrationId,
						sourceMessagebus,
						messageBus,
						false,
						messageType,
						null));
					await messageBus.Register(messageType, sourceMessagebus.SendPort, registrationId);
				}
				else if (isBroadcastRegistration)
				{
					_registrations.Insert(new Tuple<long, IMessageBusBase, IMessageBusBase, bool, Type, IAddress>(
						registrationId,
						sourceMessagebus,
						messageBus,
						true,
						null,
						null));
					await messageBus.RegisterBroadcast(sourceMessagebus.SendPort, registrationId);
				}
			}
		}

	    public void RemoveBus(IMessageBusBase messageBus)
	    {
			if (_deactivated)
				return;

			if ( messageBus == _voidBus)
				return;

			BusListener busListener;

		    lock (_sync)
		    {
			    busListener = _busListeners.FirstOrDefault(bus=>bus.MessageBus==messageBus);
				_busListeners.RemoveWhere(bus=>bus.MessageBus == messageBus);
		    }

			busListener?.Deactivate();
		}

		public void Deactivate()
		{
			if (_deactivated)
				return;
			_deactivated = true;
			IEnumerable<BusListener> busListeners;

			lock (_sync)
			{
				busListeners = _busListeners.ToList();
				_busListeners.Clear();
			}

			foreach (var busListener in busListeners)
			{
				busListener.Deactivate();
			}
		}

		#endregion
    }
}
