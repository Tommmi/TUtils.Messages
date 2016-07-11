using System;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus.Messages;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusRegisterByAddressMessage : IBusRegisterByAddressMessage
	{
		public BusRegisterByAddressMessage(IAddress destinationAddress, long registrationId, long queueId)
		{
			DestinationAddress = destinationAddress;
			RegistrationId = registrationId;
			QueueId = queueId;
		}

		public IAddress DestinationAddress { get; }
		public long RegistrationId { get; }
		public long QueueId { get; }

		public override string ToString()
		{
			return $"dest:{DestinationAddress}, queueID:{QueueId}";
		}
	}
}