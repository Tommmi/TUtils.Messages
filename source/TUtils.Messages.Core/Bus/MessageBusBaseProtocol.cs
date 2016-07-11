using System;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.Bus.Messages;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Core.Bus.Messages;

namespace TUtils.Messages.Core.Bus
{
	public class MessageBusBaseProtocol : IMessageBusBaseProtocol
	{
		IBusNameRequestMessage IMessageBusBaseProtocol.CreateBusNameRequestMessage()
		{
			return new BusNameRequestMessage();
		}

		IBusNameResponseMessage IMessageBusBaseProtocol.CreateBusNameResponseMessage(string name)
		{
			return new BusNameResponseMessage(name);
		}

		IBusRegisterBroadcastMessage IMessageBusBaseProtocol.CreateBusRegisterBroadcastMessage(long registrationId, long queueId)
		{
			return new BusRegisterBroadcastMessage(registrationId, queueId);
		}

		IBusRegisterByAddressMessage IMessageBusBaseProtocol.CreateBusRegisterByAddressMessage(long registrationId, long queueId, IAddress destinationAddress)
		{
			return new BusRegisterByAddressMessage(destinationAddress,registrationId, queueId);
		}

		IBusRegisterByTypeMessage IMessageBusBaseProtocol.CreateBusRegisterByTypeMessage(long registrationId, long queueId, Type messageType)
		{
			return new BusRegisterByTypeMessage(messageType,registrationId, queueId);
		}

		IBusUnregisterMessage IMessageBusBaseProtocol.CreateBusUnregisterMessage(long registrationId)
		{
			return new BusUnregisterMessage(registrationId);
		}

		IBusWaitForIdleRequest IMessageBusBaseProtocol.CreateBusWaitForIdleRequest()
		{
			return new BusWaitForIdleRequest();
		}

		IBusWaitForIdleResponse IMessageBusBaseProtocol.CreateBusWaitForIdleResponse(bool succeeded)
		{
			return new BusWaitForIdleResponse(succeeded);
		}

		IBusRegisterBridgeMessage IMessageBusBaseProtocol.CreateRegisterBridgeMessage(long bridgeId)
		{
			return new BusRegisterBridgeMessage(bridgeId);
		}

		IBusUnregisterBridgeMessage IMessageBusBaseProtocol.CreateUnregisterBridgeMessage(long bridgeId)
		{
			return new BusUnregisterBridgeMessage(bridgeId);
		}
	}
}
