using System;
using TUtils.Messages.Common.Bridge;

namespace TUtils.Messages.Core.Bridges
{
	[Serializable]
	public class BridgeRegisterTypeMessage : IBridgeRegisterTypeMessage
	{
		public long BridgeId { get; }
		public Type MessageType { get; }
		public long RegistrationId { get; }

		public BridgeRegisterTypeMessage(Type messageType, long registrationId, long bridgeId)
		{
			MessageType = messageType;
			RegistrationId = registrationId;
			BridgeId = bridgeId;
		}
	}
}