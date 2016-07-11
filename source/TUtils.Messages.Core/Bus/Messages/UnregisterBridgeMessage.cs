using System;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusUnregisterBridgeMessage : IBusUnregisterBridgeMessage
	{
		public BusUnregisterBridgeMessage(long bridgeId)
		{
			BridgeId = bridgeId;
		}

		public long BridgeId { get; }
	}
}