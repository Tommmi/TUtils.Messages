using System;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusWaitForIdleResponse : IBusWaitForIdleResponse
	{
		public bool Succeeded { get; }

		public BusWaitForIdleResponse(bool succeeded)
		{
			Succeeded = succeeded;
		}
	}
}