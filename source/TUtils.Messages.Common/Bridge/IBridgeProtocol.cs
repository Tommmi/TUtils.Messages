using System;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Bridge
{
	public interface IBridgeProtocol
	{
		IBridgeRegisterAddressMessage CreateRegisterAddressMessage(IAddress destinationAddress, long registrationId, long bridgeId);
		IBridgeRegisterTypeMessage CreateRegisterTypeMessage(Type messageType, long registrationId, long bridgeId);
		IBridgeRegisterBroadcastMessage CreateRegisterBroadcastMessage(long registrationId, long bridgeId);
		IBridgeUnregisterMessage CreateUnregisterMessage(long registrationId, long bridgeId);
	}
}