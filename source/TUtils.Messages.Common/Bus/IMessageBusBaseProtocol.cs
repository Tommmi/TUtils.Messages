using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUtils.Messages.Common.Bus.Messages;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Bus
{
	public interface IMessageBusBaseProtocol
	{
		IBusNameRequestMessage CreateBusNameRequestMessage();
		IBusNameResponseMessage CreateBusNameResponseMessage(string name);
		IBusRegisterBroadcastMessage CreateBusRegisterBroadcastMessage(long registrationId, long queueId);
		IBusRegisterByAddressMessage CreateBusRegisterByAddressMessage(long registrationId, long queueId, IAddress destinationAddress);
		IBusRegisterByTypeMessage CreateBusRegisterByTypeMessage(long registrationId, long queueId, Type messageType);
		IBusUnregisterMessage CreateBusUnregisterMessage(long registrationId);
		IBusWaitForIdleRequest CreateBusWaitForIdleRequest();
		IBusWaitForIdleResponse CreateBusWaitForIdleResponse(bool succeeded);
		IBusRegisterBridgeMessage CreateRegisterBridgeMessage(long bridgeId);
		IBusUnregisterBridgeMessage CreateUnregisterBridgeMessage(long bridgeId);
	}
}
