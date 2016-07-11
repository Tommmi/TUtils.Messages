using System;
using TUtils.Common.Security.Symmetric.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	public class CryptedMessage : ICryptedMessage
	{
		private readonly Guid _symmetricCryptSessionId;
		private readonly EncryptedData _data;

		public CryptedMessage(Guid symmetricCryptSessionId, EncryptedData data)
		{
			_symmetricCryptSessionId = symmetricCryptSessionId;
			_data = data;
		}

		Guid ICryptedMessage.SymmetricCryptSessionId => _symmetricCryptSessionId;

		EncryptedData ICryptedMessage.Data => _data;
	}
}