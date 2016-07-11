using TUtils.Messages.Common;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Core.Common;

namespace TUtils.Messages.Core
{
	public class AddressGenerator : IAddressGenerator
	{
		private int _lastNumber;
		private object _lock = new object();

		IAddress IAddressGenerator.Create(string nodeName)
		{
			lock (_lock)
			{
				return new Address(nodeName + " " + _lastNumber++);
			}
		}
	}
}