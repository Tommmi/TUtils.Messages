using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common.Net
{
	public interface INetNodeAddress : IAddress
	{
		string GetAsString();
	}
}