using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Common;
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
	public class BusProxy : IMessageBusBase, IDisposable
	{
		#region fields

		private readonly IQueueEntry _queueToMessageBus;
		private readonly IQueueExit _queueToBusProxy;
		private readonly IMessageBusBaseProtocol _messageBusBaseProtocol;
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		/// <summary>
		/// ({registration id, self-generated queue id, registered queue for handling messages)
		/// </summary>
		private readonly IndexedTable<long, long, IQueueEntry> _registrations = new IndexedTable<long, long, IQueueEntry>();
		/// <summary>
		/// {self-generated bridge id, registered bridge}
		/// </summary>
		private readonly IndexedTable<long, IBridge> _bridges = new IndexedTable<long, IBridge>();
		private readonly object _sync = new object();
		private string _busName = "unknown";
		private readonly List<TaskCompletionSource<bool>> _waitForIdleTasks = new List<TaskCompletionSource<bool>>();
		private CancellationTokenRegistration? _cancellationRegistration;
		private readonly List<Action> _actionsAfterCancellation = new List<Action>();

		#endregion

		#region constructor

		public BusProxy(
			IQueueEntry queueToMessageBus, 
			IQueueExit queueToBusProxy,
			IMessageBusBaseProtocol messageBusBaseProtocol,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			CancellationToken cancellationToken)
		{
			_queueToMessageBus = queueToMessageBus;
			_queueToBusProxy = queueToBusProxy;
			_messageBusBaseProtocol = messageBusBaseProtocol;
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_cancellationRegistration = cancellationToken.Register(OnCancel);
#pragma warning disable 4014
			Run().LogExceptions();
#pragma warning restore 4014
		}

		#endregion

		#region Methods / Members

		private void LogMessage(object msg, [CallerMemberName] string memberName = "")
		{
			this.Log().LogInfo(map: () => new
			{
				msgType = msg.GetType().Name,
				msgSource = (msg as IAddressedMessage)?.Source.ToString() ?? "",
				destination = (msg as IAddressedMessage)?.Destination.ToString() ?? ""
			},
			memberName: memberName);
		}

		private async Task Run()
		{
			await _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusNameRequestMessage());

			while (true)
			{
				var msg = await _queueToBusProxy.Dequeue();

				LogMessage(msg);

				if ( msg is IBusWaitForIdleResponse)
				{
					bool succeeded = (msg as IBusWaitForIdleResponse).Succeeded;

					lock (_sync)
					{
						foreach (var waitForIdleTask in _waitForIdleTasks)
						{
							if (succeeded)
								waitForIdleTask.TrySetResult(true);
							else
								waitForIdleTask.TrySetCanceled();
						}
						_waitForIdleTasks.Clear();
					}
				}
				else if ( msg is IEnqueueRequest)
				{
					var request = (IEnqueueRequest) msg;
					var internalMsg = request.Message;
					var queueId = request.QueueId;
					Task waitOnEnqueue = null;
					lock (_sync)
					{
						var queue = _registrations.FindByItem2(queueId).FirstOrDefault()?.Item3;
						if (queue != null)
							waitOnEnqueue = queue.Enqueue(internalMsg);
					}

					if (waitOnEnqueue != null)
						await waitOnEnqueue;
				}
				else if (msg is IBridgeRegisterAddressMessage)
				{
					var message = (IBridgeRegisterAddressMessage)msg;
					var address = message.DestinationAddress;
					var registrationId = message.RegistrationId;
					var bridgeId = message.BridgeId;
					IBridge bridge;
					lock (_sync)
					{
						bridge = _bridges.FindByItem1(bridgeId).FirstOrDefault()?.Item2;
					}
					bridge?.OnRegister(address, registrationId);
				}
				else if (msg is IBridgeRegisterTypeMessage)
				{
					var message = (IBridgeRegisterTypeMessage)msg;
					var type = message.MessageType;
					var registrationId = message.RegistrationId;
					var bridgeId = message.BridgeId;
					IBridge bridge;
					lock (_sync)
					{
						bridge = _bridges.FindByItem1(bridgeId).FirstOrDefault()?.Item2;
					}
					bridge?.OnRegister(type, registrationId);
				}
				else if (msg is IBridgeRegisterBroadcastMessage)
				{
					var message = (IBridgeRegisterBroadcastMessage)msg;
					var registrationId = message.RegistrationId;
					var bridgeId = message.BridgeId;
					IBridge bridge;
					lock (_sync)
					{
						bridge = _bridges.FindByItem1(bridgeId).FirstOrDefault()?.Item2;
					}
					bridge?.OnRegisterBroadcast(registrationId);
				}
				else if (msg is IBridgeUnregisterMessage)
				{
					var message = (IBridgeUnregisterMessage)msg;
					var registrationId = message.RegistrationId;
					var bridgeId = message.BridgeId;
					IBridge bridge;
					lock (_sync)
					{
						bridge = _bridges.FindByItem1(bridgeId).FirstOrDefault()?.Item2;
					}
					bridge?.OnUnregister(registrationId);
				}
				else if (msg is IBusNameResponseMessage)
				{
					_busName = (msg as IBusNameResponseMessage).BusName;
				}
			}
			// ReSharper disable once FunctionNeverReturns
		}

		private void OnCancel()
		{
			if ( !_cancellationRegistration.HasValue )
				return;
			_cancellationRegistration.Value.Dispose();
			_cancellationRegistration = null;
			List<Action> actionsAfterCancellation;
			lock (_sync)
			{
				actionsAfterCancellation = _actionsAfterCancellation.ToList();
				_actionsAfterCancellation.Clear();
			}
			foreach (var action in actionsAfterCancellation)
			{
				action();
			}
		}

		/// <summary>
		/// Creates id for queue and stores mapping
		/// </summary>
		/// <param name="destinationQueue"></param>
		/// <param name="registrationId"></param>
		/// <returns>queue ID</returns>
		private long StoreQueueEntry(IQueueEntry destinationQueue, long registrationId)
		{
			var queueId = _uniqueTimeStampCreator.Create();
			lock (_sync)
			{
				_registrations.Insert(new Tuple<long, long, IQueueEntry>(registrationId,queueId,destinationQueue));
			}
			return queueId;
		}

		#endregion

		#region IMessageBusBase

		IQueueEntry IMessageBusBase.SendPort => _queueToMessageBus;

		Task IMessageBusBase.Register(IAddress destinationAddress, IQueueEntry destinationQueue, long registrationId)
		{
			var queueId = StoreQueueEntry(destinationQueue, registrationId);
			return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusRegisterByAddressMessage(registrationId,queueId,destinationAddress));
		}

		Task IMessageBusBase.Register(Type messageType, IQueueEntry destinationQueue, long registrationId)
		{
			var queueId = StoreQueueEntry(destinationQueue, registrationId);
			return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusRegisterByTypeMessage(registrationId, queueId, messageType));
		}

		Task IMessageBusBase.RegisterBroadcast(IQueueEntry destinationQueue, long registrationId)
		{
			var queueId = StoreQueueEntry(destinationQueue, registrationId);
			return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusRegisterBroadcastMessage(registrationId, queueId));
		}

		Task IMessageBusBase.Unregister(long registrationId)
		{
			lock (_sync)
			{
				_registrations.RemoveAllMatchingItem1(registrationId);
			}
			return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusUnregisterMessage(registrationId));
		}

		Task IMessageBusBase.RegisterBridge(IBridge bridge)
		{
			var bridgeId = _uniqueTimeStampCreator.Create();
			lock (_sync)
			{
				_bridges.Insert(new Tuple<long, IBridge>(bridgeId,bridge));
			}
			return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateRegisterBridgeMessage(bridgeId));
		}

		Task IMessageBusBase.UnregisterBridge(IBridge bridge)
		{
			long? bridgeId;
			lock (_sync)
			{
				bridgeId =_bridges.FindByItem2(bridge).FirstOrDefault()?.Item1;
				_bridges.RemoveAllMatchingItem2(bridge);
			}
			if ( bridgeId.HasValue)
				return _queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateUnregisterBridgeMessage(bridgeId.Value));
			return Task.CompletedTask;
		}

#pragma warning disable 1998
		async Task<string> IMessageBusBase.GetBusName()
#pragma warning restore 1998
		{
			return _busName;
		}

		Task IMessageBusBase.WaitForIdle()
		{
			var tcs = new TaskCompletionSource<bool>();

			Action actionAfterCancellation = () =>
			{
				lock (_sync)
				{
					_waitForIdleTasks.Remove(tcs);
				}
				tcs.TrySetCanceled();
			};

			lock (_sync)
			{
				_waitForIdleTasks.Add(tcs);
				_actionsAfterCancellation.Add(actionAfterCancellation);
			}

			_queueToMessageBus.Enqueue(_messageBusBaseProtocol.CreateBusWaitForIdleRequest());

			return tcs.Task;
		}

		#endregion

		public void Dispose()
		{
			OnCancel();
		}
	}
}
