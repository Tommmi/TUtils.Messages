using System;

namespace TUtils.Messages.Common.Bridge
{
	public interface IBridgeRegisterTypeMessage
	{
		long BridgeId { get; }
		Type MessageType { get; }
		long RegistrationId { get; }
	}
}