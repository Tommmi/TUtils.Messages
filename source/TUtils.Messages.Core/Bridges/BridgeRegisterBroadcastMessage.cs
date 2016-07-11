using System;
using TUtils.Messages.Common.Bridge;

namespace TUtils.Messages.Core.Bridges
{
	[Serializable]
	public class BridgeRegisterBroadcastMessage : IBridgeRegisterBroadcastMessage
	{
		public long BridgeId { get; }
		public long RegistrationId { get; }

		public BridgeRegisterBroadcastMessage(long registrationId, long bridgeId)
		{
			RegistrationId = registrationId;
			BridgeId = bridgeId;
		}
	}
}