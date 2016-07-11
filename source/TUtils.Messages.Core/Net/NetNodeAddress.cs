using System;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Core.Common;

namespace TUtils.Messages.Core.Net
{
	[Serializable]
#pragma warning disable 660,661
	public class NetNodeAddress : Address, INetNodeAddress
#pragma warning restore 660,661
	{
		protected bool Equals(NetNodeAddress other)
		{
			return base.Equals(other);
		}

		public NetNodeAddress(Uri uri) : base(uri.ToString())
		{
		}

		public NetNodeAddress(string uri) : base(uri)
		{
		}

		public static bool operator ==(NetNodeAddress o1, NetNodeAddress o2)
		{
			if (ReferenceEquals(o1, null))
				return ReferenceEquals(o2, null);
			if (ReferenceEquals(o2, null))
				return false;
			return o1.Equals(o2);
		}

		public static bool operator !=(NetNodeAddress o1, NetNodeAddress o2)
		{
			return !(o1 == o2);
		}

		string INetNodeAddress.GetAsString()
		{
			return ToString();
		}
	}
}
