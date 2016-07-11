using System;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Core.Common
{
	[Serializable]
	public class Address : IAddress
	{
		private readonly string _address;

		public Address(string address)
		{
			_address = address;
		}

		int IAddress.Hash => _address.GetHashCode();

		bool IAddress.IsEqual(IAddress otherAddress)
		{
			var address = otherAddress as Address;
			if (!ReferenceEquals(address,null))
				return _address == address._address;

			return false;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as Address);
		}

		protected bool Equals(Address other)
		{
			return string.Equals(_address, other._address);
		}

		public override int GetHashCode()
		{
			return _address?.GetHashCode() ?? 0;
		}

		public override string ToString()
		{
			return _address;
		}

		public static bool operator ==(Address o1, Address o2)
		{
			if (ReferenceEquals(o1, null))
				return ReferenceEquals(o2, null);
			if (ReferenceEquals(o2, null))
				return false;
			return o1.Equals(o2);
		}

		public static bool operator !=(Address o1, Address o2)
		{
			return !(o1 == o2);
		}
	}
}