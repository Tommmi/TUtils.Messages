using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Logging;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core.Queue
{
	public abstract class QueueAdapterBase : IQueueTail, IDisposable
	{
		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		#region fields

		private bool _cancel;
		private readonly IQueue _queueOutBuffer;
		private CancellationTokenRegistration? _cancellationRegistration;
		private readonly  object _sync = new object();
		private readonly List<Action<object>> _actionsOnDequeueing = new List<Action<object>>();
		private readonly List<Action<object>> _actionsOnEnqueueing = new List<Action<object>>();
		private readonly List<Action> _actionsAfterCancellation = new List<Action>();

		#endregion

		#region constructor

		protected QueueAdapterBase(
			ITLog logger,
			IQueueFactory queueFactory,
			IQueueEntry queueEntry,
			IQueueExit queueExit,
			CancellationToken cancellationToken)
		{
			Logger = logger;
			QueueEntry = queueEntry;
			QueueExit = queueExit;
			_queueOutBuffer = queueFactory.Create();
			CancellationToken = cancellationToken;
			_cancellationRegistration = cancellationToken.Register(OnCancel);

#pragma warning disable 4014
			Run().LogExceptions(logger);
#pragma warning restore 4014
		}

		#endregion

		#region private methods

		private void OnCancel()
		{
			_cancellationRegistration?.Dispose();
			_cancellationRegistration = null;
		}

		private async Task Run()
		{
			while (!_cancel)
			{
				CancellationToken.ThrowIfCancellationRequested();
				var msg = await QueueExit.Dequeue();
				List<Action<object>> actionsOnReceiving;
				lock (_sync)
				{
					actionsOnReceiving = _actionsOnDequeueing.ToList();
				}
				foreach (var actionOnReceiving in actionsOnReceiving)
					actionOnReceiving(msg);

#pragma warning disable 4014
				DequeueHook(msg).LogExceptions(Logger);
#pragma warning restore 4014
			}
		}

		private Task<TimeoutResult<TMessage>> WaitOnMessage<TMessage>(int timeoutMs, List<Action<object>> actionsOnMessage, Func<TMessage,bool> filter)
		{
			var tcs = new TaskCompletionSource<TimeoutResult<TMessage>>();
			Action onCancel = null;
			Action<object> onMessage = null;

			onMessage = msg =>
			{
				if (msg is TMessage && (filter==null || filter((TMessage)msg)))
				{
					lock (_sync)
					{
						actionsOnMessage.Remove(onMessage);
						// ReSharper disable once AccessToModifiedClosure
						_actionsAfterCancellation.Remove(onCancel);
					}

					tcs.TrySetResult(new TimeoutResult<TMessage>((TMessage)msg, timeoutElapsed: false));
				}
			};
			onCancel = () =>
			{
				lock (_sync)
				{
					actionsOnMessage.Remove(onMessage);
					// ReSharper disable once AccessToModifiedClosure
					_actionsAfterCancellation.Remove(onCancel);
				}

				tcs.TrySetCanceled();
			};

			lock (_sync)
			{
				_actionsAfterCancellation.Add(onCancel);
				actionsOnMessage.Add(onMessage);
			}

			if (timeoutMs > 0)
			{
#pragma warning disable 4014
				StartTimer(timeoutMs, onMessage, onCancel, tcs, actionsOnMessage).LogExceptions(Logger);
#pragma warning restore 4014
			}

			return tcs.Task;
		}

		private async Task StartTimer<TMessage>(
			int timeoutMs, 
			Action<object> onMessage, 
			Action onCancel, 
			TaskCompletionSource<TimeoutResult<TMessage>> tcs,
			List<Action<object>> actionsOnMessage)
		{
			await Task.Delay(timeoutMs, CancellationToken);
			lock (_sync)
			{
				actionsOnMessage.Remove(onMessage);
				_actionsAfterCancellation.Remove(onCancel);
			}
			tcs.TrySetResult(new TimeoutResult<TMessage>(value: default(TMessage), timeoutElapsed: false));
		}

		#endregion

		#region overrideable

		/// <summary>
		/// Called whenever a message has been inserted on the other side.
		/// DequeueHook() should call ProceedDequeue(msg) to enqueue the received message into the output buffer queue.
		/// If not the message would be ignored completely.
		/// Inside DequeueHook() you may enqueue other messages and wait for response by calling
		/// ProceedEnqueue() and WaitOnxxx().
		/// </summary>
		/// <param name="msg"></param>
		protected virtual Task DequeueHook(object msg)
		{
			return ProceedDequeue(msg);
		}

		/// <summary>
		/// Called whenever method Enqueue() has been called.
		/// EnqueueHook() should call ProceedEnqueue(msg) to insert the message into the queue. 
		/// If not, the message would be ignored.
		/// </summary>
		/// <param name="msg"></param>
		// ReSharper disable once VirtualMemberNeverOverriden.Global
		protected virtual Task EnqueueHook(object msg)
		{
			return ProceedEnqueue(msg);
		}

		#endregion

		#region protected

		// ReSharper disable once MemberCanBePrivate.Global
		protected ITLog Logger { get; }
		// ReSharper disable once MemberCanBePrivate.Global
		protected IQueueEntry QueueEntry { get; }
		// ReSharper disable once MemberCanBePrivate.Global
		protected IQueueExit QueueExit { get; }
		// ReSharper disable once MemberCanBePrivate.Global
		protected CancellationToken CancellationToken { get; }

		/// <summary>
		/// Inserts message into output buffer queue. 
		/// method Dequeue() gets messages from that output buffer queue only.
		/// </summary>
		/// <param name="msg"></param>
		/// <returns></returns>
		protected Task ProceedDequeue(object msg)
		{
			return _queueOutBuffer.Entry.Enqueue(msg);
		}

		/// <summary>
		/// Inserts message into the underlying queue. 
		/// </summary>
		/// <param name="msg"></param>
		/// <returns></returns>
		protected Task ProceedEnqueue(object msg)
		{
			return QueueEntry.Enqueue(msg);
		}

		// ReSharper disable once UnusedMember.Global
		/// <summary>
		/// waits for at maximum "timeoutMs" milliseconds for a dequeued message of type {TMessage} matching the given filter "filter".
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="timeoutMs"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnReceivingMessage<TMessage>(int timeoutMs, Func<TMessage, bool> filter)
		{
			return WaitOnMessage(timeoutMs, _actionsOnDequeueing, filter: filter);
		}

		// ReSharper disable once UnusedMember.Global

		// ReSharper disable once UnusedMember.Global
		/// <summary>
		/// waits endless for a dequeued message of type {TMessage} matching the given filter "filter".
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="filter"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnReceivingMessage<TMessage>(Func<TMessage, bool> filter)
		{
			return WaitOnMessage(timeoutMs: 0, actionsOnMessage: _actionsOnDequeueing, filter: filter);
		}

		// ReSharper disable once UnusedMember.Global
		/// <summary>
		/// waits for at maximum "timeoutMs" milliseconds for a dequeued message of type {TMessage}
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="timeoutMs"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnReceivingMessage<TMessage>(int timeoutMs)
		{
			return WaitOnMessage<TMessage>(timeoutMs, _actionsOnDequeueing, filter: null);
		}

		// ReSharper disable once UnusedMember.Global
		/// <summary>
		/// waits endless for a dequeued message of type {TMessage}.
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnReceivingMessage<TMessage>()
		{
			return WaitOnMessage<TMessage>(timeoutMs: 0, actionsOnMessage: _actionsOnDequeueing, filter: null);
		}

		/// <summary>
		/// waits for at maximum "timeoutMs" milliseconds for a enqueued message of type {TMessage} matching the given filter "filter".
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="timeoutMs"></param>
		/// <param name="filter"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnSendingMessage<TMessage>(int timeoutMs, Func<TMessage, bool> filter)
		{
			return WaitOnMessage(timeoutMs, _actionsOnEnqueueing, filter: filter);
		}

		/// <summary>
		/// waits endless for a enqueued message of type {TMessage} matching the given filter "filter".
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="filter"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnSendingMessage<TMessage>(Func<TMessage, bool> filter)
		{
			return WaitOnMessage(timeoutMs: 0, actionsOnMessage: _actionsOnEnqueueing, filter: filter);
		}

		/// <summary>
		/// waits for at maximum "timeoutMs" milliseconds for a enqueued message of type {TMessage}.
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <param name="timeoutMs"></param>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnSendingMessage<TMessage>(int timeoutMs)
		{
			return WaitOnMessage<TMessage>(timeoutMs, _actionsOnEnqueueing, filter: null);
		}

		// ReSharper disable once UnusedMember.Global
		/// <summary>
		/// waits endless for a enqueued message of type {TMessage}.
		/// </summary>
		/// <typeparam name="TMessage"></typeparam>
		/// <returns></returns>
		protected Task<TimeoutResult<TMessage>> WaitOnSendingMessage<TMessage>()
		{
			return WaitOnMessage<TMessage>(timeoutMs: 0, actionsOnMessage: _actionsOnEnqueueing, filter: null);
		}

		#endregion

		#region IQueueEntry / IQueueExit / IDisposable

		Task IQueueEntry.Enqueue(object msg)
		{
			List<Action<object>> actionsOnSending;

			lock (_sync)
			{
				actionsOnSending = _actionsOnEnqueueing.ToList();
			}

			foreach (var action in actionsOnSending)
				action(msg);

#pragma warning disable 4014
			EnqueueHook(msg).LogExceptions(Logger);
#pragma warning restore 4014
			return Task.CompletedTask;
		}

		Task<object> IQueueExit.Dequeue()
		{
			return _queueOutBuffer.Exit.Dequeue();
		}

		Task<TimeoutResult<object>> IQueueExit.Dequeue(int timeoutMs)
		{
			return _queueOutBuffer.Exit.Dequeue(timeoutMs);
		}

		object IQueueExit.Peek()
		{
			return _queueOutBuffer.Exit.Peek();
		}

		public void Dispose()
		{
			_cancel = true;
			OnCancel();
		}

		#endregion
	}
}