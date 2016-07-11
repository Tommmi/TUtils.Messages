using System;
using TUtils.Messages.Common.Common;

namespace TUtils.Messages.Common
{
	public interface IBridge
	{
		void OnRegister(IAddress destinationAddress, long registrationId);
		void OnRegister(Type messageType, long registrationId);
		void OnRegisterBroadcast(long registrationId);
		void OnUnregister(long registrationId);
	}
}
