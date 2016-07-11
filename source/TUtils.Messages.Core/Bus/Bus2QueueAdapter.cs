using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Bus.Messages;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Bus
{
	public class Bus2QueueAdapter
	{
		#region types

		private class BridgeProxy : IBridge
		{
			private readonly long _bridgeId;
			private readonly IQueueEntry _queue2BusProxy;
			private readonly IBridgeProtocol _bridgeProtocol;

			public BridgeProxy(
				long bridgeId,
				IQueueEntry queue2BusProxy,
				IBridgeProtocol bridgeProtocol)
			{
				_bridgeId = bridgeId;
				_queue2BusProxy = queue2BusProxy;
				_bridgeProtocol = bridgeProtocol;
			}

			void IBridge.OnRegister(IAddress destinationAddress, long registrationId)
			{
				_queue2BusProxy.Enqueue(_bridgeProtocol.CreateRegisterAddressMessage(destinationAddress, registrationId,
					_bridgeId));
			}

			void IBridge.OnRegister(Type messageType, long registrationId)
			{
				_queue2BusProxy.Enqueue(_bridgeProtocol.CreateRegisterTypeMessage(messageType, registrationId, _bridgeId));
			}

			void IBridge.OnRegisterBroadcast(long registrationId)
			{
				_queue2BusProxy.Enqueue(_bridgeProtocol.CreateRegisterBroadcastMessage(registrationId, _bridgeId));
			}

			void IBridge.OnUnregister(long registrationId)
			{
				_queue2BusProxy.Enqueue(_bridgeProtocol.CreateUnregisterMessage(registrationId, _bridgeId));
			}
		}

		private class QueueProxy : IQueueEntry
		{
			private readonly long _queueId;
			private readonly IQueueEntry _queue2BusProxy;
			private readonly IQueueEntryProtocol _queueEntryProtocol;

			public QueueProxy(
				long queueId, 
				IQueueEntry queue2BusProxy,
				IQueueEntryProtocol queueEntryProtocol)
			{
				_queueId = queueId;
				_queue2BusProxy = queue2BusProxy;
				_queueEntryProtocol = queueEntryProtocol;
			}

			public Task Enqueue(object msg)
			{
				var wrappedMsg = _queueEntryProtocol.CreateEnqueueRequest(_queueId, msg);
				return _queue2BusProxy.Enqueue(wrappedMsg);
			}
		}

		#endregion

		#region fields

		private readonly IMessageBusBase _messageBus;
		private readonly IQueueEntry _queue2BusProxy;
		private readonly IQueueExit _queue2MessageBus;
		private readonly CancellationToken _cancellationToken;
		private readonly IQueueEntryProtocol _queueEntryProtocol;
		private readonly IMessageBusBaseProtocol _messageBusBaseProtocol;
		private readonly IBridgeProtocol _bridgeProtocol;
		private readonly ITLog _logger;

		/// <summary>
		/// Tuples of (registration ID, queue ID, queue)
		/// </summary>
		private readonly IndexedTable<long, long, QueueProxy> _queues = new IndexedTable<long, long, QueueProxy>();
		private readonly IndexedTable<long, BridgeProxy> _bridges = new IndexedTable<long, BridgeProxy>();
		private readonly object _sync = new object();

		#endregion

		#region constructor

		public Bus2QueueAdapter(
			IMessageBusBase messageBus,
			IQueueEntry queue2BusProxy,
			IQueueExit queue2MessageBus,
			CancellationToken cancellationToken,
			IQueueEntryProtocol queueEntryProtocol,
			IMessageBusBaseProtocol messageBusBaseProtocol,
			IBridgeProtocol bridgeProtocol,
			ITLog logger)
		{
			_messageBus = messageBus;
			_queue2BusProxy = queue2BusProxy;
			_queue2MessageBus = queue2MessageBus;
			_cancellationToken = cancellationToken;
			_queueEntryProtocol = queueEntryProtocol;
			_messageBusBaseProtocol = messageBusBaseProtocol;
			_bridgeProtocol = bridgeProtocol;
			_logger = logger;
			Task.Run(()=>Run().LogExceptions(logger),cancellationToken);
		}

		#endregion

		#region private methods

		private void LogMessage(object msg)
		{
			if (_logger.IsActive(LogSeverityEnum.INFO, this))
			{
				string loggingText = $"message {msg.GetType().Name}";

				var message = msg as IAddressedMessage;
				if (message != null)
				{
					loggingText += $" source:{message.Source}, destination:{message.Destination}";
				}
				loggingText += $" content:{msg}";
				_logger.LogInfo(this, loggingText);
			}
		}


		private async Task Run()
		{
			while (true)
			{
				var msg = await _queue2MessageBus.Dequeue();

				LogMessage(msg);

				if (msg is IBusNameRequestMessage)
				{
					var busName = await _messageBus.GetBusName();
					await _queue2BusProxy.Enqueue(_messageBusBaseProtocol.CreateBusNameResponseMessage(busName));
				}
				else if (msg is IBusRegisterBroadcastMessage)
				{
					var message = (IBusRegisterBroadcastMessage) msg;
					var queueId = message.QueueId;
					var registrationId = message.RegistrationId;
					var queueAdapter = StoreQueueAdapter(queueId, registrationId);
					await _messageBus.RegisterBroadcast(queueAdapter, registrationId);
				}
				else if (msg is IBusRegisterByAddressMessage)
				{
					var message = (IBusRegisterByAddressMessage) msg;
					var queueId = message.QueueId;
					var registrationId = message.RegistrationId;
					var address = message.DestinationAddress;
					var queueAdapter = StoreQueueAdapter(queueId, registrationId);
					await _messageBus.Register(address, queueAdapter, registrationId);
				}
				else if (msg is IBusRegisterByTypeMessage)
				{
					var message = (IBusRegisterByTypeMessage) msg;
					var queueId = message.QueueId;
					var registrationId = message.RegistrationId;
					var messageType = message.MessageType;
					var queueAdapter = StoreQueueAdapter(queueId, registrationId);
					await _messageBus.Register(messageType, queueAdapter, registrationId);
				}
				else if (msg is IBusUnregisterMessage)
				{
					var message = (IBusUnregisterMessage)msg;
					var registrationId = message.RegistrationId;

					lock (_sync)
					{
						_queues.RemoveAllMatchingItem1(registrationId);
					}
					await _messageBus.Unregister(registrationId);
				}
				else if (msg is IBusRegisterBridgeMessage)
				{
					var message = (IBusRegisterBridgeMessage)msg;
					var bridgeId = message.BridgeId;
					var bridgeProxy = new BridgeProxy(bridgeId, _queue2BusProxy, _bridgeProtocol);

					lock (_sync)
					{
						_bridges.Insert(new Tuple<long, BridgeProxy>(bridgeId, bridgeProxy));
					}
					await _messageBus.RegisterBridge(bridgeProxy);
				}
				else if (msg is IBusUnregisterBridgeMessage)
				{
					var message = (IBusUnregisterBridgeMessage)msg;
					var bridgeId = message.BridgeId;
					BridgeProxy bridgeProxy;

					lock (_sync)
					{
						bridgeProxy = _bridges.FindByItem1(bridgeId).FirstOrDefault()?.Item2;
						_bridges.RemoveAllMatchingItem1(bridgeId);
					}

					if ( bridgeProxy != null )
						await _messageBus.UnregisterBridge(bridgeProxy);
				}
				else if (msg is IBusWaitForIdleRequest)
				{
#pragma warning disable 4014
					 _messageBus.WaitForIdle().LogExceptions(_logger).ContinueWith(task =>
#pragma warning restore 4014
					 {
						 _queue2BusProxy.Enqueue(_messageBusBaseProtocol.CreateBusWaitForIdleResponse(task.IsCompleted));
					 }, _cancellationToken);
				}
				else
				{
					await _messageBus.SendPort.Enqueue(msg);
				}
			}
			// ReSharper disable once FunctionNeverReturns
		}

		private QueueProxy StoreQueueAdapter(long queueId, long registrationId)
		{
			QueueProxy queueProxy = new QueueProxy(queueId, _queue2BusProxy, _queueEntryProtocol);
			lock (_sync)
			{
				_queues.Insert(new Tuple<long, long, QueueProxy>(registrationId, queueId, queueProxy));
			}
			return queueProxy;
		}

		#endregion
	}
}
