namespace TUtils.Messages.Common.Bus.Messages
{
	public interface IBusRegisterBroadcastMessage
	{
		long RegistrationId { get; }
		long QueueId { get; }
	}
}