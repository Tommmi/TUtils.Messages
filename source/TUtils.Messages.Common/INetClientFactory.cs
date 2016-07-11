using System;
using TUtils.Messages.Common.Net;

namespace TUtils.Messages.Common
{
	public interface INetClientFactory
	{
		INetClient Create(Uri serverAddress);
	}
}