using System;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Core.Bridges
{
	[Serializable]
	public class BridgeRegisterAddressMessage : IBridgeRegisterAddressMessage
	{
		public long BridgeId { get; }
		public IAddress DestinationAddress { get; }
		public long RegistrationId { get; }

		public BridgeRegisterAddressMessage(long registrationId, IAddress destinationAddress, long bridgeId)
		{
			RegistrationId = registrationId;
			DestinationAddress = destinationAddress;
			BridgeId = bridgeId;
		}
	}
}