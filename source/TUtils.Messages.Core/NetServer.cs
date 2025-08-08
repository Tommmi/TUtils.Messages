using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Bus;

namespace TUtils.Messages.Core
{
	public class NetServer : INetServer
	{
		#region fields

		/// <summary>
		/// {client node id, queue to bus, queue to client, bus2QueueAdapter}
		/// </summary>
		private readonly IndexedTable<INetNodeAddress,IQueue, IQueue, Bus2QueueAdapter> _queues = new IndexedTable<INetNodeAddress, IQueue, IQueue, Bus2QueueAdapter>();
		private readonly IClientLoadBalancing _clientLoadBalancing;
		private readonly IMessageSerializer _serializer;
		private readonly IQueueFactory _queueFactory;
		private readonly Func<int> _getTimeoutForLongPollingRequest;
		private readonly IMessageBusBase _messageBus;
		private readonly CancellationToken _cancellationToken;
		private readonly IQueueEntryProtocol _queueEntryProtocol;
		private readonly IMessageBusBaseProtocol _messageBusBaseProtocol;
		private readonly IBridgeProtocol _bridgeProtocol;
		private readonly object _sync = new object();

		#endregion

		#region constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="clientLoadBalancing">
		/// ClientLoadBalancing or NoClientLoadBalancing
		/// </param>
		/// <param name="serializer"> </param>
		/// <param name="queueFactory"></param>
		/// <param name="messageBus"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="queueEntryProtocol"></param>
		/// <param name="messageBusBaseProtocol"></param>
		/// <param name="bridgeProtocol"></param>
		/// <param name="logger"></param>
		/// <param name="getTimeoutForLongPollingRequest">
		/// after how many milliseconds at maximum should INetServer.OnDequeue() return with a response ?
		/// </param>
		public NetServer(
			IClientLoadBalancing clientLoadBalancing,
			IMessageSerializer serializer,
			IQueueFactory queueFactory,
			IMessageBusBase messageBus,
			CancellationToken cancellationToken,
			IQueueEntryProtocol queueEntryProtocol,
			IMessageBusBaseProtocol messageBusBaseProtocol,
			IBridgeProtocol bridgeProtocol,
			Func<int> getTimeoutForLongPollingRequest)
		{
			_clientLoadBalancing = clientLoadBalancing;
			_serializer = serializer;
			_queueFactory = queueFactory;
			_getTimeoutForLongPollingRequest = getTimeoutForLongPollingRequest;
			_messageBus = messageBus;
			_cancellationToken = cancellationToken;
			_queueEntryProtocol = queueEntryProtocol;
			_messageBusBaseProtocol = messageBusBaseProtocol;
			_bridgeProtocol = bridgeProtocol;
		}

		#endregion

		#region Member

		private Tuple<INetNodeAddress,IQueue, IQueue, Bus2QueueAdapter> EnsureQueue(INetNodeAddress source)
		{
			lock (_sync)
			{
				var queueInfo = _queues.FindByItem1(source).FirstOrDefault();
				if (queueInfo != null)
					return queueInfo;
			}


			// don't call it in sync lock !
			var queueToBus = _queueFactory.Create();
			var queueToClient = _queueFactory.Create();


			lock (_sync)
			{
				var queueInfo = _queues.FindByItem1(source).FirstOrDefault();

				if (queueInfo == null)
				{
					queueInfo = new Tuple<INetNodeAddress, IQueue, IQueue, Bus2QueueAdapter>(
						source,
						queueToBus,
						queueToClient,
						new Bus2QueueAdapter(
							_messageBus,
							queueToClient.Entry,
							queueToBus.Exit,
							_cancellationToken,
							_queueEntryProtocol,
							_messageBusBaseProtocol,
							_bridgeProtocol));
					_queues.Insert(queueInfo);
				}
				return queueInfo;
			}
		}

		#endregion

		#region IDisposable

		void IDisposable.Dispose()
		{
			IEnumerable<Tuple<INetNodeAddress, IQueue, IQueue, Bus2QueueAdapter>> allQueues;

			lock (_sync)
			{
				allQueues = _queues.GetAllRows();
			}

			foreach (var queue in allQueues)
			{
				// ReSharper disable once SuspiciousTypeConversion.Global
				var busAdapter = queue.Item4 as IDisposable;
				// ReSharper disable once SuspiciousTypeConversion.Global
				var queue1 = queue.Item2 as IDisposable;
				// ReSharper disable once SuspiciousTypeConversion.Global
				var queue2 = queue.Item3 as IDisposable;
				busAdapter?.Dispose();
				queue1?.Dispose();
				queue2?.Dispose();
			}
		}

		#endregion

		#region INetServer

		async Task<EnqueueResponse> INetServer.OnEnqueue(
			INetNodeAddress source, 
			IPAddress ipAddress, 
			Func<Task<MessageContent>> getContent)
		{
			if (!await _clientLoadBalancing.MayReceiveRequest(ipAddress))
				return new EnqueueResponse(ResponseEnum.Timeout);

			var content = await getContent();
			var queueInfo = EnsureQueue(source);
			var queueToBus = queueInfo.Item2;
			var message = _serializer.Deserialize(content);
			await queueToBus.Entry.Enqueue(message);
			return new EnqueueResponse(ResponseEnum.Succeeded);
		}

		async Task<DequeueResponse> INetServer.OnDequeue(
			INetNodeAddress source, 
			IPAddress ipAddress)
		{
			if (!await _clientLoadBalancing.MayReceiveRequest(ipAddress))
				return new DequeueResponse(ResponseEnum.Timeout, null);

			var queueInfo = EnsureQueue(source);
			var queueToClient = queueInfo.Item3;
			var res = await queueToClient.Exit.Dequeue(_getTimeoutForLongPollingRequest());
			if (res.TimeoutElapsed)
				return new DequeueResponse(ResponseEnum.Timeout, null);
			var messageContent = _serializer.Serialize(res.Value);

			return new DequeueResponse(ResponseEnum.Succeeded, messageContent);
		}

		#endregion
	}
}
