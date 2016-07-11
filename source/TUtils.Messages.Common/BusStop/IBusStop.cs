using System;
using System.Threading.Tasks;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;

namespace TUtils.Messages.Common.BusStop
{
	public interface IBusStop
	{
		/// <summary>
		/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
		/// a message of type TResponse has been received with same request id "request.RequestId".
		/// request.RequestId and request.Source will be set by this method Send() automatically.
		/// </summary>
		/// <typeparam name="TRequest"></typeparam>
		/// <typeparam name="TResponse"></typeparam>
		/// <param name="request"></param>
		/// <returns></returns>
		Task<TResponse> Send<TRequest, TResponse>(TRequest request)
			where TRequest : IRequestMessage
			where TResponse : IResponseMessage;

		/// <summary>
		/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
		/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
		/// elapsed (see constructor parameter).
		/// request.RequestId and request.Source will be set by this method Send() automatically.
		/// SendWithTimeoutAndRetry() will retry to send the request up to 10 times within the given timeout.
		/// The polling intervall time will be increased be each retry, bur won't be smaller than 100 ms.
		/// </summary>
		/// <typeparam name="TRequest"></typeparam>
		/// <typeparam name="TResponse"></typeparam>
		/// <param name="request"></param>
		/// <returns></returns>
		Task<TimeoutResult<TResponse>> SendWithTimeoutAndRetry<TRequest, TResponse>(TRequest request)
			where TRequest : IRequestMessage
			where TResponse : IResponseMessage;

		/// <summary>
		/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
		/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
		/// elapsed (see constructor parameter).
		/// request.RequestId and request.Source will be set by this method Send() automatically.
		/// This method starts only one request.
		/// </summary>
		/// <typeparam name="TRequest"></typeparam>
		/// <typeparam name="TResponse"></typeparam>
		/// <param name="request"></param>
		/// <returns></returns>
		Task<TimeoutResult<TResponse>> SendWithTimeout<TRequest, TResponse>(TRequest request)
			where TRequest : IRequestMessage
			where TResponse : IResponseMessage;

		/// <summary>
		/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
		/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
		/// elapsed.
		/// request.RequestId and request.Source will be set by this method Send() automatically.
		/// This method starts only one request.
		/// </summary>
		/// <typeparam name="TRequest"></typeparam>
		/// <typeparam name="TResponse"></typeparam>
		/// <param name="request"></param>
		/// <param name="timeoutMs"></param>
		/// <returns></returns>
		Task<TimeoutResult<TResponse>> SendWithTimeout<TRequest, TResponse>(TRequest request, long timeoutMs)
			where TRequest : IRequestMessage
			where TResponse : IResponseMessage;

		/// <summary>
		/// Posts message "message" into bus.
		/// If "message" implements interface IAddressedMessage, this method will set property message.Source automatically.
		/// Returned task completes, if message could be inserted into send queue.
		/// Full queues would prevent Post() to complete.
		/// </summary>
		/// <param name="message"></param>
		/// <returns></returns>
		void Post(object message);
		Task<TMessage> WaitOnMessageToMe<TMessage>(Func<TMessage, bool> filter) where TMessage : IAddressedMessage;
		Task<TMessage> WaitOnMessageToMe<TMessage>() where TMessage : IAddressedMessage;
		Task<TimeoutResult<TMessage>> WaitOnMessageToMe<TMessage>(long timeoutMs, Func<TMessage, bool> filter) where TMessage : IAddressedMessage;
		Task<TimeoutResult<TMessage>> WaitOnMessageToMe<TMessage>(long timeoutMs) where TMessage : IAddressedMessage;
		IAddress BusStopAddress { get; }
		IMessageBus MessageBus { get; }
		IBusStopOn<TMessageType> On<TMessageType>();
		Task WaitForIdle();
	}
}