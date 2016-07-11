using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Messages
{
	public interface IAddressedMessage
	{
		IAddress Destination { get; }
		IAddress Source { get; set; }
	}
}