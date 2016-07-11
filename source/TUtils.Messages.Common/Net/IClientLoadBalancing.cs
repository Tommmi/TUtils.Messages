using System;
using System.Net;
using System.Threading.Tasks;

namespace TUtils.Messages.Common.Net
{
	/// <summary>
	/// If several clients compete for server, IClientLoadBalancing allows to 
	/// slowdown requests from clients which are making many requests per second.
	/// This also helps to protect against DDOS attacks, which are so sneaky, that 
	/// firwalls can't identify them as attacks, because they act like absolute normal
	/// users. 
	/// </summary>
	public interface IClientLoadBalancing
	{
		/// <summary>
		/// waits till server may proceed to handle the request
		/// </summary>
		/// <param name="sender"></param>
		/// <returns>false, if request is blocked</returns>
		Task<bool> MayReceiveRequest(
			IPAddress sender);
	}
}
