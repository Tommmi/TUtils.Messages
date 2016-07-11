using System;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusUnregisterMessage : IBusUnregisterMessage
	{
		public BusUnregisterMessage(long registrationId)
		{
			RegistrationId = registrationId;
		}

		public long RegistrationId { get; }
	}
}