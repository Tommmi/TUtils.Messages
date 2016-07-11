using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TUtils.Messages.Common.Net;

namespace TUtils.Messages.Core.Net
{
	public class NoClientLoadBalancing : IClientLoadBalancing
	{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		async Task<bool> IClientLoadBalancing.MayReceiveRequest(IPAddress sender)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			return true;
		}
	}
}
