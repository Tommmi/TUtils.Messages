using System;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusRegisterBroadcastMessage : IBusRegisterBroadcastMessage
	{
		public BusRegisterBroadcastMessage(long registrationId, long queueId)
		{
			RegistrationId = registrationId;
			QueueId = queueId;
		}

		public long RegistrationId { get; }
		public long QueueId { get; }
	}
}