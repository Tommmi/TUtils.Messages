using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Extensions;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;

namespace TUtils.Messages.Core.BusStop
{
	public class BusStop : IBusStop, IDisposable
	{
		private const int MinTimeoutForOnePollingRequestMs = 100;
		#region fields

		private IAddress _address;
		private readonly List<Action> _afterCancellationJobs = new List<Action>();
		private IMessageBus _bus;
		private CancellationTokenRegistration? _cancelRegistration;
		private readonly List<Action<IAddressedMessage>> _internalActionHandlers = new List<Action<IAddressedMessage>>();
		private readonly List<Func<object,Task>> _internalTaskHandlers = new List<Func<object, Task>>();
		private readonly object _sync = new object();
		private IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private CancellationToken _cancellationToken;
		private ISystemTimeProvider _time;
		private long _defaultTimeoutMs;
		private long _firstIntervallTime;

		#endregion

		#region constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bus"></param>
		/// <param name="address"></param>
		/// <param name="uniqueTimeStampCreator"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="time"></param>
		/// <param name="defaultTimeoutMs"> 
		/// </param>
		/// <returns></returns>
		public async Task<IBusStop> Init(IMessageBus bus,
			IAddress address,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			CancellationToken cancellationToken,
			ISystemTimeProvider time,
			long defaultTimeoutMs)
		{
			_bus = bus;
			_address = address;
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_cancellationToken = cancellationToken;
			_time = time;
			_firstIntervallTime = GetFirstIntervallTime(defaultTimeoutMs);
			_defaultTimeoutMs = defaultTimeoutMs;
			_cancelRegistration = cancellationToken.Register(OnCancel);
			await _bus.Register(address, AsyncMessageHandler);
			return this;
		}

		#endregion

		#region methods

		private async Task AsyncMessageHandler(IAddressedMessage addressedMessage)
		{
			List<Func<object, Task>> internalTaskHandlers;
			List<Action<IAddressedMessage>> internalActionHandlers;
			lock (_sync)
			{
				internalActionHandlers = _internalActionHandlers.ToList();
				internalTaskHandlers = _internalTaskHandlers.ToList();
			}

			foreach (var listener in internalActionHandlers)
			{
				listener(addressedMessage);
			}

			await Task.WhenAll(internalTaskHandlers.Select(handler => handler(addressedMessage)));
		}

		private long FillRequestIdAndSource<TRequest>(TRequest request)
			where TRequest : IRequestMessage
		{
			var requestId = _uniqueTimeStampCreator.Create();
			request.RequestId = requestId;
			request.Source = _address;
			return requestId;
		}

		private void OnCancel()
		{
			if (_cancelRegistration == null )
				return;

			_cancelRegistration?.Dispose();
			_cancelRegistration = null;

			List<Action> afterCancellationJobsCopy;

			lock (_sync)
			{
				afterCancellationJobsCopy = _afterCancellationJobs.ToList();
				_afterCancellationJobs.Clear();
			}

			foreach (var action in afterCancellationJobsCopy)
			{
				action();
			}
		}

		internal void RegisterHandlerInternal(Func<object, Task> handler)
		{
			lock (_sync)
			{
				_internalTaskHandlers.Add(handler);
			}
		}

		internal void UnregisterHandlerInternal(Func<object, Task> handler)
		{
			lock (_sync)
			{
				_internalTaskHandlers.RemoveWhere(h=>h == handler);
			}
		}

		#endregion

		#region IBusStop

		IAddress IBusStop.BusStopAddress => _address;

		IMessageBus IBusStop.MessageBus => _bus;

		Task<TResponse> IBusStop.Send<TRequest, TResponse>(TRequest request)
		{
			var requestId = FillRequestIdAndSource(request);
			var waitTask = (this as IBusStop).WaitOnMessageToMe<TResponse>(msg => msg.RequestId == requestId);
			_bus.SendPort.Enqueue(request);
			return waitTask;
		}

		private Task<TimeoutResult<TResponse>> SendWithTimeoutInternal<TRequest, TResponse>(TRequest request, long timeoutMs)
			where TRequest : IRequestMessage
			where TResponse : IResponseMessage
		{
			var requestId = FillRequestIdAndSource(request);
			var waitTask = (this as IBusStop).WaitOnMessageToMe<TResponse>(timeoutMs, msg => msg.RequestId == requestId);
			_bus.SendPort.Enqueue(request);
			return waitTask;
		}

		Task<TimeoutResult<TResponse>> IBusStop.SendWithTimeout<TRequest, TResponse>(TRequest request)
		{
			return SendWithTimeoutInternal<TRequest, TResponse>(request, _defaultTimeoutMs);
		}

		Task<TimeoutResult<TResponse>> IBusStop.SendWithTimeout<TRequest, TResponse>(TRequest request, long timeoutMs)
		{
			return SendWithTimeoutInternal<TRequest, TResponse>(request, timeoutMs);
		}

		async Task<TimeoutResult<TResponse>> IBusStop.SendWithTimeoutAndRetry<TRequest, TResponse>(TRequest request)
		{
			TimeoutResult<TResponse> res = null;
			var startTime = _time.LocalTime;
			var endTime = startTime + new TimeSpan(_defaultTimeoutMs*TimeSpan.TicksPerMillisecond);
			var currentIntervallTime = _firstIntervallTime;
			while (_time.LocalTime < endTime)
			{
				res = await SendWithTimeoutInternal<TRequest, TResponse>(request, currentIntervallTime);
				if (!res.TimeoutElapsed)
					return res;
				currentIntervallTime = GetCurrentIntervallTime(startTime);
			}
			return res;
		}

		private static long GetFirstIntervallTime(long defaultTimeoutMs)
		{
			const int countIterations = 10;
			var dt0 = defaultTimeoutMs / Math.Pow(3, countIterations);
			var firstWaitTime = Convert.ToInt64(Math.Floor(dt0));
			if (firstWaitTime < MinTimeoutForOnePollingRequestMs)
				firstWaitTime = MinTimeoutForOnePollingRequestMs;
			return firstWaitTime;
		}

		private long GetCurrentIntervallTime(DateTime startTime)
		{
			var now = _time.LocalTime;
			var waitedTime = Convert.ToInt64(Math.Floor((now - startTime).TotalMilliseconds));
			var newWaitTime = 2 * waitedTime;
			if (newWaitTime + waitedTime > _defaultTimeoutMs)
				newWaitTime = _defaultTimeoutMs - waitedTime;
			return newWaitTime;
		}

		IBusStopOn<TMessageType> IBusStop.On<TMessageType>()
		{
			return new BusStopOn<TMessageType>(_bus, this, _cancellationToken);
		}

		void IBusStop.Post(object message)
		{
			var addressedMessage = message as IAddressedMessage;
			if (addressedMessage != null)
				addressedMessage.Source = _address;

#			pragma warning disable 4014
			_bus.SendPort.Enqueue(message);
#			pragma warning restore 4014
		}

		async Task<TMessage> IBusStop.WaitOnMessageToMe<TMessage>(Func<TMessage, bool> filter)
		{
			var waitResult = await ((IBusStop) this).WaitOnMessageToMe(timeoutMs: 0, filter: filter);
			return waitResult.Value;
		}

		Task<TimeoutResult<TMessage>> IBusStop.WaitOnMessageToMe<TMessage>(long timeoutMs)
		{
			return ((IBusStop)this).WaitOnMessageToMe<TMessage>(timeoutMs, msg => true);
		}

		Task<TMessage> IBusStop.WaitOnMessageToMe<TMessage>()
		{
			return ((IBusStop) this).WaitOnMessageToMe<TMessage>(msg => true);
		}

		Task<TimeoutResult<TMessage>> IBusStop.WaitOnMessageToMe<TMessage>(long timeoutMs, Func<TMessage, bool> filter)
		{
			var tcs = new TaskCompletionSource<TimeoutResult<TMessage>>();

			Action<IAddressedMessage> handler = null;

			Action afterCancellationJob = () =>
			{
				tcs.TrySetCanceled();
				lock (_sync)
				{
					// ReSharper disable once AccessToModifiedClosure
					_internalActionHandlers.Remove(handler);
				}
			};

			if (timeoutMs > 0)
			{
				var timeoutTask = Task.Delay(new TimeSpan(ticks:timeoutMs * TimeSpan.TicksPerMillisecond), _cancellationToken);
				timeoutTask.ContinueWith(task =>
				{
					lock (_sync)
					{
						// ReSharper disable once AccessToModifiedClosure
						_internalActionHandlers.Remove(handler);
						_afterCancellationJobs.Remove(afterCancellationJob);
					}
					tcs.TrySetResult(new TimeoutResult<TMessage>(default(TMessage), true));
				}, _cancellationToken);
			}

			handler = msg =>
			{
				if (msg is TMessage && filter((TMessage)msg))
				{
					lock (_sync)
					{
						// ReSharper disable once AccessToModifiedClosure
						_internalActionHandlers.Remove(handler);
						_afterCancellationJobs.Remove(afterCancellationJob);
					}
					tcs.TrySetResult(new TimeoutResult<TMessage>((TMessage)msg,false));
				}
			};

			lock (_sync)
			{
				_afterCancellationJobs.Add(afterCancellationJob);
				_internalActionHandlers.Add(handler);
			}

			return tcs.Task;
		}

		Task IBusStop.WaitForIdle()
		{
			return _bus.WaitForIdle();
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			if (_cancelRegistration == null)
				return;
			_cancelRegistration?.Dispose();
			_cancelRegistration = null;

		}

		#endregion
	}
}