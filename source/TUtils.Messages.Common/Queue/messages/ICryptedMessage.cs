using System;
using TUtils.Common.Security.Symmetric.Common;

namespace TUtils.Messages.Common.Queue.messages
{
	public interface ICryptedMessage
	{
		Guid SymmetricCryptSessionId { get; }
		EncryptedData Data { get; }
	}
}