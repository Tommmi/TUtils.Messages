using System;

namespace TUtils.Messages.Common.Bus.Messages
{
	public interface IBusRegisterByTypeMessage
	{
		Type MessageType { get; }
		long RegistrationId { get; }
		long QueueId { get; }
	}
}