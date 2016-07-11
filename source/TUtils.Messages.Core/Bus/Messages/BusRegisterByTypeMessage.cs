using System;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusRegisterByTypeMessage : IBusRegisterByTypeMessage
	{
		public BusRegisterByTypeMessage(Type messageType, long registrationId, long queueId)
		{
			MessageType = messageType;
			RegistrationId = registrationId;
			QueueId = queueId;
		}

		public Type MessageType { get; }
		public long RegistrationId { get; }
		public long QueueId { get; }
	}
}