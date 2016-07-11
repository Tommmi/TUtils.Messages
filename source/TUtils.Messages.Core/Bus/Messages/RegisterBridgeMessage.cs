using System;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusRegisterBridgeMessage : IBusRegisterBridgeMessage
	{
		public BusRegisterBridgeMessage(long bridgeId)
		{
			BridgeId = bridgeId;
		}

		public long BridgeId { get; }
	}
}