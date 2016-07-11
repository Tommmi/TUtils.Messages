namespace TUtils.Messages.Common.Bridge
{
	public interface IBridgeRegisterBroadcastMessage
	{
		long BridgeId { get; }
		long RegistrationId { get; }
	}
}