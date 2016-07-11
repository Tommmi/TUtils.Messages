using System;
using System.Threading.Tasks;

namespace TUtils.Messages.Common.Net
{
	public interface INetClient : IDisposable
	{
		Task<NetActionResultEnum> Enqueue(byte[] data);
		Task<NetActionResultEnum> Enqueue(string data);
		Task<NetClientDequeueResult> Dequeue();
	}
}