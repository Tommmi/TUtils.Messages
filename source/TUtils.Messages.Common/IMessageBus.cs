using System;
using System.Threading.Tasks;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Common
{
	public interface IMessageBus : IMessageBusBase
	{
		/// <summary>
		/// registers a handler for address based messages
		/// </summary>
		/// <param name="destinationAddress"></param>
		/// <param name="asyncMessageHandler"></param>
		Task Register(IAddress destinationAddress, Func<IAddressedMessage, Task> asyncMessageHandler);
		/// <summary>
		/// registers a queue destination for address based messages.
		/// </summary>
		/// <param name="destinationAddress"></param>
		/// <param name="destinationQueue"></param>
		Task Register(IAddress destinationAddress, IQueueEntry destinationQueue);
		/// <summary>
		/// registers a handler for messages with the given type
		/// </summary>
		/// <param name="messageType"></param>
		/// <param name="asyncMessageHandler"></param>
		Task Register(Type messageType, Func<object, Task> asyncMessageHandler);
		/// <summary>
		/// registers a queue destination for messages  with the given type
		/// </summary>
		/// <param name="messageType"></param>
		/// <param name="destinationQueue"></param>
		Task Register(Type messageType, IQueueEntry destinationQueue);
		/// <summary>
		/// registers a handler for messages with the given type
		/// </summary>
		/// <typeparam name="TMessageType"></typeparam>
		/// <param name="asyncMessageHandler"></param>
		Task Register<TMessageType>(Func<object, Task> asyncMessageHandler);
		/// <summary>
		/// registers a queue destination for messages  with the given type
		/// </summary>
		/// <typeparam name="TMessageType"></typeparam>
		/// <param name="destinationQueue"></param>
		Task Register<TMessageType>(IQueueEntry destinationQueue);
		/// <summary>
		/// registers a queue destination for all messages
		/// </summary>
		/// <param name="destinationQueue"></param>
		Task RegisterBroadcast(IQueueEntry destinationQueue);
		/// <summary>
		/// registers a handler for all messages
		/// </summary>
		/// <param name="asyncMessageHandler"></param>
		Task RegisterBroadcast(Func<object, Task> asyncMessageHandler);
		/// <summary>
		/// unregister all registrations associated with the given handler
		/// </summary>
		/// <param name="asyncMessageHandler"></param>
		Task Unregister(Func<object, Task> asyncMessageHandler);
		/// <summary>
		/// unregister all registrations associated with the given handler
		/// </summary>
		/// <param name="asyncMessageHandler"></param>
		Task Unregister(Func<IAddressedMessage, Task> asyncMessageHandler);
		/// <summary>
		/// unregister all registrations associated with the given destination queue
		/// </summary>
		/// <param name="destinationQueue"></param>
		Task Unregister(IQueueEntry destinationQueue);
	}
}