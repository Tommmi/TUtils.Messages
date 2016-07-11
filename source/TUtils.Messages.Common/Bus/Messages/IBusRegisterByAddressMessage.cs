using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Bus.Messages
{
	public interface IBusRegisterByAddressMessage
	{
		IAddress DestinationAddress { get; }
		long RegistrationId { get; }
		long QueueId { get; }
	}
}