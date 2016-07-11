namespace TUtils.Messages.Common.Bridge
{
	public interface IBridgeUnregisterMessage
	{
		long BridgeId { get; }
		long RegistrationId { get; }
	}
}