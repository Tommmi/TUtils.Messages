using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;

namespace TUtils.Messages.Core.Test
{
	[Serializable]
	public class TestRequestMessage : IRequestMessage
	{
		public string Value { get; }

		public IAddress Destination { get; }

		IAddress IAddressedMessage.Source { get; set; }

		long IRequestMessage.RequestId { get; set; }

		public TestRequestMessage(IAddress destination, string value)
		{
			Value = value;
			Destination = destination;
		}
	}

	[Serializable]
	public class TestResponseMessage : IResponseMessage
	{
		public IAddress Destination { get; }

		IAddress IAddressedMessage.Source { get; set; }

		long IResponseMessage.RequestId { get; set; }

		public string Value { get; }

		public TestResponseMessage(TestRequestMessage requestMessage)
		{
			var request = (IRequestMessage) requestMessage;
			var @this = (IResponseMessage) this;
			Destination = request.Source;
			@this.RequestId = request.RequestId;
			Value = requestMessage.Value;
		}
	}
}
