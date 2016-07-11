using System;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Extensions;
using TUtils.Common.Logging;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.Net
{
	public class NetClientQueue : IQueueTail, IDisposable
	{
		private readonly IMessageSerializer _serializer;
		private readonly ITLog _logger;
		private readonly ISystemTimeProvider _time;
		private INetClient _client;
		private DateTime _lastDequeueTrial = new DateTime(0);

		private readonly int _requestRetryIntervallTimeMs;



		/// <summary>
		/// 
		/// </summary>
		/// <param name="clientFactory"></param>
		/// <param name="serializer"></param>
		/// <param name="logger"></param>
		/// <param name="time"></param>
		/// <param name="uri"></param>
		/// <param name="requestRetryIntervallTimeMs">
		/// how many milli seconds should the client wait at minimum between two failed polling requests ?
		/// Note ! if a server isn't available the client will retry to connect it with this 
		/// intervall time. This value hasn't any effect on the time between two successfully requests.
		/// The long polling timeout shouldn't be smaller than requestRetryIntervallTimeMs !
		/// </param>
		public NetClientQueue(
			INetClientFactory clientFactory,
			IMessageSerializer serializer,
			ITLog logger,
			ISystemTimeProvider time,
			Uri uri, 
			int requestRetryIntervallTimeMs)
		{
			_serializer = serializer;
			_logger = logger;
			_time = time;
			_requestRetryIntervallTimeMs = requestRetryIntervallTimeMs;
			_client = clientFactory.Create(uri);
		}

		async Task IQueueEntry.Enqueue(object msg)
		{
			var data = _serializer.Serialize(msg).GetData();
			var res = await _client.Enqueue(data);

			if ( res == NetActionResultEnum.Succeeded)
				return;

			_logger.LogInfo(this,"message not delivered {0}", ()=>res);
		}

		async Task<object> IQueueExit.Dequeue()
		{
			var res = await ((IQueueExit)this).Dequeue(0);
			return res.Value;
		}

		async Task<TimeoutResult<object>> IQueueExit.Dequeue(int timeoutMs)
		{
			if (timeoutMs == 0)
				timeoutMs = int.MaxValue;
			else if (timeoutMs < 0)
				return new TimeoutResult<object>(null, timeoutElapsed: true);

			var startTimeOfRequest = _time.LocalTime;
			_lastDequeueTrial = startTimeOfRequest;

			var res = await _client.Dequeue();

			if (res.Result == NetActionResultEnum.Succeeded)
				return new TimeoutResult<object>(_serializer.Deserialize(res.MessageContent),timeoutElapsed:false);

			var now = _time.LocalTime;
			int timeSinceLastDequeue = Math.Round(now.Subtract(_lastDequeueTrial).TotalMilliseconds).ToInt32();
			int timeSinceRequestStart = timeSinceLastDequeue;
			int minTimeToWaitDueToIntervallTime = _requestRetryIntervallTimeMs - timeSinceLastDequeue;
			int maxTimeToWaitDueToTimeout = timeoutMs - timeSinceRequestStart;

			// if timeoutElapsed
			if (timeSinceRequestStart > timeoutMs
				|| maxTimeToWaitDueToTimeout < minTimeToWaitDueToIntervallTime)
			{
				return new TimeoutResult<object>(null, timeoutElapsed: true);
			}

			if (minTimeToWaitDueToIntervallTime > 0)
			{
				await Task.Delay(minTimeToWaitDueToIntervallTime);
				now = _time.LocalTime;
				timeSinceRequestStart = Math.Round(now.Subtract(startTimeOfRequest).TotalMilliseconds).ToInt32();
				maxTimeToWaitDueToTimeout = timeoutMs - timeSinceRequestStart;
			}

			_lastDequeueTrial = now;

			return await ((IQueueExit)this).Dequeue(maxTimeToWaitDueToTimeout);
		}

		object IQueueExit.Peek()
		{
			return  ((IQueueExit)this).Peek();
		}

		public void Dispose()
		{
			_client?.Dispose();
			_client = null;

		}
	}
}
