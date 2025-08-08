using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Async;
using TUtils.Common.Common;
using TUtils.Common.Logging;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Net;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace TUtils.Messages.Core.Net
{
	/// <summary>
	/// implementation of IClientLoadBalancing.
	/// If several clients compete for server, IClientLoadBalancing allows to 
	/// slowdown requests from clients which are making many requests per minute.
	/// This also helps to protect against DDOS attacks, which are so sneaky, that 
	/// firewalls can't identify them as attacks, because they act like absolute normal
	/// users. 
	/// </summary>
	/// <remarks>
	/// Normally ClientLoadBalancing doesn't slowdown the call of IClientLoadBalancing.MayReceiveRequest.
	/// But if the CPU-performance of the server is so low, that a lower priorized thread
	/// can't work fast enough to pump messages, than requests will be queued. 
	/// The request queue inside ClientLoadBalancing orders requests by priority of it's client ip's 
	/// request rate. So requests from IPs, which have made a normal count of requests in the last minute will
	/// pass requests from other IPs. If the queue is bigger than a configurable amount of value
	/// then it will be shorten and requests from frequently requesting clients will be discarded.
	/// 
	/// How it works:
	/// IClientLoadBalancing.MayReceiveRequest creates a TaskCompletionSource object (TCS) for the incoming request
	/// and puts it into a queue storage (called step 1 level) .
	/// A normal priorized thread (TaskPump1) pumps these TCSs into second queue storage (called step 2 level).
	/// A lower priorized thread (TaskPum2) dequeues the TCSs of the step 2 level queue and completes them, so that 
	/// the associated await statements of MayReceiveRequest() complete, too.
	/// If there are too much TSCs in a queue, the lower priorized items will be removed and the associated TSCs will 
	/// be completed with result value false.
	/// 
	/// Why that complicate two-level queueing ?
	/// There must be a lower-priorized thread to detect overloading of server.
	/// The lower priorized thread TaskPump2 locks an object (_sync2) for reading the level-2 queue.
	/// So all threads which are waiting for inserting an item into the level-2 queue could be blocked for a long time.
	/// IClientLoadBalancing.MayReceiveRequest must not block the calling thread for a long time !
	/// It's just an async method which should return at once. 
	/// So that's the part of TaskPump1: Since it's a normal-priorized thread, it's reading from level-1 queue
	/// doesn't block any other thread when inserting items into level-1 queue.
	/// On the other hand TaskPump1 stops running for a long time, when it is inserting items into level-2 queue. But that's ok,
	/// because no one else needs the thread of TaskPump1. Of course TaskPump1 may not locking _sync1, when it is about to lock _sync2.
	/// </remarks>
	public class ClientLoadBalancing : IClientLoadBalancing, IDisposable
	{
		#region types

		private class WaitingTask : IComparable<WaitingTask>, IEquatable<WaitingTask>
		{
			private readonly double _requestsPerMinuteRate;
			private readonly long _id;
			public TaskCompletionSource<bool> Tcs { get; }

			public WaitingTask(
				double requestsPerMinuteRate,
				long id,
				TaskCompletionSource<bool> tcs)
			{
				_requestsPerMinuteRate = requestsPerMinuteRate;
				_id = id;
				Tcs = tcs;
			}

			public int CompareTo(WaitingTask other)
			{
				if (_requestsPerMinuteRate < other._requestsPerMinuteRate)
					return -1;
				if (_requestsPerMinuteRate > other._requestsPerMinuteRate)
					return 1;
				if (_id < other._id)
					return -1;
				if (_id > other._id)
					return 1;
				return 0;
			}

			public override bool Equals(object obj)
			{
				return Equals(obj as WaitingTask);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (_requestsPerMinuteRate.GetHashCode()*397) ^ _id.GetHashCode();
				}
			}

			public bool Equals(WaitingTask other)
			{
				if (ReferenceEquals(null, other)) return false;
				if (ReferenceEquals(this, other)) return true;

				return
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					_requestsPerMinuteRate == other._requestsPerMinuteRate
					&& _id == other._id;
			}


			public static bool operator ==(WaitingTask o1, WaitingTask o2)
			{
				if (ReferenceEquals(o1, null))
					return ReferenceEquals(o2, null);
				if (ReferenceEquals(o2, null))
					return false;
				return o1.Equals(o2);
			}

			public static bool operator !=(WaitingTask o1, WaitingTask o2)
			{
				return !(o1 == o2);
			}
		}

		private class ClientInfo
		{
			private readonly ISystemTimeProvider _timeProvider;
			private DateTime _startTimeMessurement;
			private double _countOfRequests;

			public ClientInfo(
				DateTime startTimeMessurement,
				ISystemTimeProvider timeProvider)
			{
				_timeProvider = timeProvider;
				_startTimeMessurement = startTimeMessurement;
				_countOfRequests = 1;
			}

			private double GetCountOfRequestsPerMinute(DateTime now)
			{
				var minutes = (now - _startTimeMessurement).TotalMinutes;
				return _countOfRequests / minutes;
			}

			private void Normalize(DateTime now)
			{
				var minutes = (now - _startTimeMessurement).TotalMinutes;
				_startTimeMessurement = now - new TimeSpan(0, minutes: 1, seconds: 0);
				_countOfRequests /= minutes;
			}

			/// <summary>
			/// Informs this management unit, that the referred client is requesting the server now.
			/// Returns the request per minute rate, before this request has been made and updates the rate afterwards.
			/// </summary>
			/// <returns></returns>
			public double TouchNow()
			{
				var now = _timeProvider.LocalTime;
				Normalize(now);
				_countOfRequests += 1;
				return GetCountOfRequestsPerMinute(now);
			}
		}

		#endregion

		#region fields

		private readonly object _sync1 = new object();
		private readonly object _sync2 = new object();
		private readonly Dictionary<IPAddress, ClientInfo> _clientInfos = new Dictionary<IPAddress, ClientInfo>();

		private readonly SortedDictionary<WaitingTask, bool> _waitingTasksStep1 = new SortedDictionary<WaitingTask, bool>();
		private readonly SortedDictionary<WaitingTask, bool> _waitingTasksStep2 = new SortedDictionary<WaitingTask, bool>();

		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private readonly ISystemTimeProvider _timeProvider;
		private readonly int _maxCountOfWaitingTasks;
		private CancellationTokenRegistration _cancelRegistration;
		private readonly AsyncEvent _taskInsertedInPump1;
		private readonly AsyncEvent _taskInsertedInPump2;

		#endregion

		#region constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <param name="logger"></param>
		/// <param name="uniqueTimeStampCreator"></param>
		/// <param name="timeProvider"></param>
		/// <param name="maxCountOfWaitingTasks">
		/// how many messages may be queued due to system performance overload, till some messages may be removed automatically ?
		/// </param>
		public ClientLoadBalancing(
			CancellationToken cancellationToken, 
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			ISystemTimeProvider timeProvider,
			int maxCountOfWaitingTasks)
		{
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_timeProvider = timeProvider;
			_maxCountOfWaitingTasks = maxCountOfWaitingTasks;
			_cancelRegistration = cancellationToken.Register(OnCanceled);
			_taskInsertedInPump1 = new AsyncEvent(cancellationToken);
			_taskInsertedInPump2 = new AsyncEvent(cancellationToken);
			AsyncThreadStarter.Start(
				threadName: "DDOSAttackShield_Step1",
				cancellationToken: cancellationToken,
				threadPriority: ThreadPriority.Normal,
				synchronousThreadMethod: TaskPump1);
			AsyncThreadStarter.Start(
				threadName: "DDOSAttackShield_Step2",
				cancellationToken: cancellationToken,
				threadPriority: ThreadPriority.Lowest,
				synchronousThreadMethod: TaskPump2);
		}

		#endregion

		#region private / Members

		/// <summary>
		/// TaskPump1 is a thread which pumps tasks from queue _waitingTasksStep1 to queue _waitingTasksStep2.
		/// TaskPump2 is a thread which handles tasks from queue _waitingTasksStep2.
		/// TaskPump2 is lower priorized. TaskPump1 is normal priorized.
		/// TaskPump2 locks _sync2 to read the tasks. When locking it, TaskPump2 prevents all other threads
		/// from runnnig, when they are waiting for _sync2, which is locked by the lower priorized thread TaskPump2.
		/// The only thread, which waits for _sync2 is TaskPump1, which may be blocked for a while.
		/// The system thread calls MayReceiveRequest() which fills queue _waitingTasksStep1 and locks _sync1.
		/// So the queue _waitingTasksStep1 will be filled independent of TaskPump2. Requests from IPs,
		/// which haven't made a request for a long time, will pass all other requests from other IPs.
		/// Since TaskPump2 is lower priorized, the server won't get many requests, when it's heavy busy.
		/// Moreover only requests from IPs which haven't made requests successfully for a long time will be prefered.
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private bool TaskPump1(CancellationToken cancellationToken)
		{
			// Note ! In a "_sync2" locked section we may lock _sync1,
			// but we may not lock _sync2, when we are in a "_sync1" locked section.
			// Otherwise we got a circular call situation ==> dead lock risk.
			// Note ! In a "_sync1" locked section we may not wait a long time.
			// This could be, if we would wait endless for _sync2-object inside a _sync1-section.
			// So we lock first _sync2 and then _sync1.
			while (true)
			{
				Task waitingForTaskInserted = null;
				cancellationToken.ThrowIfCancellationRequested();

				lock (_sync2)
				{
					lock (_sync1)
					{
						var count = _waitingTasksStep1.Count;
						if (count == 0)
						{
							waitingForTaskInserted = _taskInsertedInPump1.RegisterForEvent();
						}
						else
						{
							foreach (var waitingTask in _waitingTasksStep1)
								_waitingTasksStep2.Add(waitingTask.Key,true);

							_waitingTasksStep1.Clear();
							this.Log().LogInfo(() => new { messageCnt = count, descr = "task pump 1" });
						}
					}
				}
				if ( waitingForTaskInserted == null )
					_taskInsertedInPump2.Rise();
				waitingForTaskInserted?.Wait(cancellationToken);
			}
			// ReSharper disable once FunctionNeverReturns
		}

		private bool TaskPump2(CancellationToken cancellationToken)
		{
			while (true)
			{
				Task waitingForTaskInserted = null;
				cancellationToken.ThrowIfCancellationRequested();
				lock (_sync2)
				{
					if (_waitingTasksStep2.Count == 0)
					{
						waitingForTaskInserted = _taskInsertedInPump2.RegisterForEvent();
					}
					else
					{
						var waitingTask = _waitingTasksStep2.First().Key;
						waitingTask.Tcs.TrySetResult(true);
						_waitingTasksStep2.Remove(waitingTask);
						ShrinkQueue(_waitingTasksStep2);
						// don't log because this blocks logger by a lower priorized thread !
						// _logger.LogInfo(this, "one message pumped (task pump 1)");
					}
				}

				waitingForTaskInserted?.Wait(cancellationToken);
			}
			// ReSharper disable once FunctionNeverReturns
		}

		private void OnCanceled()
		{
			lock (_sync1)
			{
				foreach (var waitingTask in _waitingTasksStep1)
					waitingTask.Key.Tcs.TrySetCanceled();

				_waitingTasksStep1.Clear();
			}

			lock (_sync2)
			{
				foreach (var waitingTask in _waitingTasksStep2)
					waitingTask.Key.Tcs.TrySetCanceled();

				_waitingTasksStep2.Clear();
			}
		}

		private void ShrinkQueue(SortedDictionary<WaitingTask, bool> queue)
		{
			var currentCountOfTasks = queue.Count;
			if (currentCountOfTasks > _maxCountOfWaitingTasks)
			{
				for (int i = 0; i < currentCountOfTasks - _maxCountOfWaitingTasks; i++)
				{
					var waitingTask = queue.Last().Key;
					queue.Remove(waitingTask);
					waitingTask.Tcs.TrySetResult(false);
				}
			}
		}

		#endregion

		#region IClientLoadBalancing

		Task<bool> IClientLoadBalancing.MayReceiveRequest(IPAddress sender)
		{
			var tcs = new TaskCompletionSource<bool>();
			var id = _uniqueTimeStampCreator.Create();

			lock (_sync1)
			{
				ClientInfo clientInfo;
				double oldRequestPerMinuteRate;

				if (!_clientInfos.TryGetValue(sender, out clientInfo))
				{
					clientInfo = new ClientInfo(_timeProvider.LocalTime, _timeProvider);
					_clientInfos[sender] = clientInfo;
					oldRequestPerMinuteRate = 0;
				}
				else
				{
					oldRequestPerMinuteRate = clientInfo.TouchNow();
				}

				_waitingTasksStep1.Add(new WaitingTask(oldRequestPerMinuteRate,id,tcs),true);
				ShrinkQueue(_waitingTasksStep1);
			}

			_taskInsertedInPump1.Rise();
			return tcs.Task;
		}

		#endregion

		#region IDisposable

		public void Dispose()
		{
			_cancelRegistration.Dispose();
			OnCanceled();
		}

		#endregion
	}
}
