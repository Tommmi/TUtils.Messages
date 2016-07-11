using System;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus.Messages;

namespace TUtils.Messages.Core.Bus.Messages
{
	[Serializable]
	public class BusNameResponseMessage : IBusNameResponseMessage
	{
		public BusNameResponseMessage(string busName)
		{
			BusName = busName;
		}

		public string BusName { get; }
	}
}