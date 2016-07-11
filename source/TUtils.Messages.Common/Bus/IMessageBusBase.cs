using System;
using System.Threading.Tasks;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Common.Bus
{
	public interface IMessageBusBase
	{
		/// <summary>
		/// send messages into the bus here
		/// </summary>
		IQueueEntry SendPort { get; }
		/// <summary>
		/// registers a queue destination for address based messages and tag that registration with the given id
		/// </summary>
		/// <param name="destinationAddress"></param>
		/// <param name="destinationQueue"></param>
		/// <param name="registrationId"></param>
		Task Register(IAddress destinationAddress, IQueueEntry destinationQueue, long registrationId);
		/// <summary>
		/// registers a queue destination for messages  with the given type and tag that registration with the given id
		/// </summary>
		/// <param name="messageType"></param>
		/// <param name="destinationQueue"></param>
		/// <param name="registrationId"></param>
		Task Register(Type messageType, IQueueEntry destinationQueue, long registrationId);
		/// <summary>
		/// registers a queue destination for all messages and tag that registration with the given id
		/// </summary>
		/// <param name="destinationQueue"></param>
		/// <param name="registrationId"></param>
		Task RegisterBroadcast(IQueueEntry destinationQueue, long registrationId);
		/// <summary>
		/// unregister all registrations tagged with the given registration id
		/// </summary>
		/// <param name="registrationId"></param>
		Task Unregister(long registrationId);
		/// <summary>
		/// Registers a bridge. A bridge joins two or more message busses.
		/// </summary>
		/// <param name="bridge"></param>
		Task RegisterBridge(IBridge bridge);
		/// <summary>
		/// Unregisters a bridge. A bridge joins two or more message busses.
		/// </summary>
		/// <param name="bridge"></param>
		Task UnregisterBridge(IBridge bridge);

		/// <summary>
		/// The given name of the bus. (Only for debugging issues)
		/// </summary>
		Task<string> GetBusName();
		/// <summary>
		/// waits till message bus isn't too busy anymore.
		/// Attention ! Be carefull to call this method.
		/// If this method is called in a message handler, it prevents the handler to complete.
		/// So waiting for idle status, could also prevent bus to get idle again. 
		/// To get around with this stuff, just call it before starting new working jobs
		/// to reduce the payload of the system. 
		/// </summary>
		/// <returns></returns>
		Task WaitForIdle();
	}
}