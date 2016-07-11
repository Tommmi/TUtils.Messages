using System;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Core.Bridges
{
	public class BridgeProtocol : IBridgeProtocol
	{
		IBridgeRegisterAddressMessage IBridgeProtocol.CreateRegisterAddressMessage(IAddress destinationAddress, long registrationId, long bridgeId)
		{
			return new BridgeRegisterAddressMessage(registrationId, destinationAddress, bridgeId);
		}

		IBridgeRegisterTypeMessage IBridgeProtocol.CreateRegisterTypeMessage(Type messageType, long registrationId, long bridgeId)
		{
			return new BridgeRegisterTypeMessage(messageType,registrationId, bridgeId);
		}

		IBridgeRegisterBroadcastMessage IBridgeProtocol.CreateRegisterBroadcastMessage(long registrationId, long bridgeId)
		{
			return new BridgeRegisterBroadcastMessage(registrationId, bridgeId);
		}

		IBridgeUnregisterMessage IBridgeProtocol.CreateUnregisterMessage(long registrationId, long bridgeId)
		{
			return new BridgeUnregisterMessage(registrationId,bridgeId);
		}
	}
}