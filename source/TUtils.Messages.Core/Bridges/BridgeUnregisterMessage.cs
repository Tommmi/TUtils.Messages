using System;
using TUtils.Messages.Common.Bridge;

namespace TUtils.Messages.Core.Bridges
{
	[Serializable]
	public class BridgeUnregisterMessage : IBridgeUnregisterMessage
	{
		public long BridgeId { get; }
		public long RegistrationId { get; }

		public BridgeUnregisterMessage(long registrationId, long bridgeId)
		{
			RegistrationId = registrationId;
			BridgeId = bridgeId;
		}
	}
}