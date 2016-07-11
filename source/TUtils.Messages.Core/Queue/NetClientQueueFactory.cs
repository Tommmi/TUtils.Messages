using System;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Net;

namespace TUtils.Messages.Core.Queue
{
	public class NetClientQueueFactory : INetClientQueueFactory
	{
		private readonly INetClientFactory _netClientFactory;
		private readonly IMessageSerializer _serializer;
		private readonly ITLog _logger;
		private readonly ISystemTimeProvider _time;
		private readonly int _requestRetryIntervallTimeMs;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="netClientFactory"></param>
		/// <param name="serializer"></param>
		/// <param name="logger"></param>
		/// <param name="time"></param>
		/// <param name="requestRetryIntervallTimeMs">
		/// how many milli seconds should the client wait at minimum between two failed polling requests ?
		/// Note ! if a server isn't available the client will retry to connect it with this 
		/// intervall time. This value hasn't any effect on the time between two successfully requests.
		/// The long polling timeout shouldn't be smaller than requestRetryIntervallTimeMs !
		/// </param>
		public NetClientQueueFactory(
			INetClientFactory netClientFactory,
			IMessageSerializer serializer,
			ITLog logger,
			ISystemTimeProvider time,
			int requestRetryIntervallTimeMs)
		{
			_netClientFactory = netClientFactory;
			_serializer = serializer;
			_logger = logger;
			_time = time;
			_requestRetryIntervallTimeMs = requestRetryIntervallTimeMs;
		}

		public IQueueTail CreateQueue(Uri serverAddress)
		{
			return new NetClientQueue(_netClientFactory,_serializer,_logger,_time, serverAddress, _requestRetryIntervallTimeMs);
		}
	}
}
