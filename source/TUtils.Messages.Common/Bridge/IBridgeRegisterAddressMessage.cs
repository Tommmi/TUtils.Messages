using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Bridge
{
	public interface IBridgeRegisterAddressMessage
	{
		long BridgeId { get; }
		/// <summary>
		/// registering for messages with this detination address
		/// </summary>
		IAddress DestinationAddress { get; }
		long RegistrationId { get; }
	}
}