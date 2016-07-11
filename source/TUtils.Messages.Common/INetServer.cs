using System;
using System.Net;
using System.Threading.Tasks;
using TUtils.Messages.Common.Net;

namespace TUtils.Messages.Common
{
	public interface INetServer : IDisposable
	{
		Task<DequeueResponse> OnDequeue(INetNodeAddress source, IPAddress ipAddress);
		Task<EnqueueResponse> OnEnqueue(INetNodeAddress source, IPAddress ipAddress, Func<Task<MessageContent>> getContent);
	}
}
