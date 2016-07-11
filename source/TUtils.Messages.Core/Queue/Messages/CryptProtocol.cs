using System;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Symmetric.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	public class CryptProtocol : ICryptProtocol
	{
		IInitCryptographic ICryptProtocol.CreateInitCryptographic(Guid assymetricCryptSessionId,
			IPublicCertContentBase64String publicCertifikate)
		{
			return new InitCryptographic(assymetricCryptSessionId,publicCertifikate);
		}

		IInitCryptographicResponse ICryptProtocol.CreateInitCryptographicResponse(
			IInitCryptographic request, 
			Guid symmetricCryptSessionId,
			byte[] encryptedSymmetricSecret,
			IPublicCertContentBase64String clientCertifikate,
			byte[] signature)
		{
			return new InitCryptographicResponse(request.AssymetricCryptSessionId,symmetricCryptSessionId,encryptedSymmetricSecret, clientCertifikate,signature);
		}

		ICryptedMessage ICryptProtocol.CreateEncryptedMessage(Guid symmetricCryptSessionId, EncryptedData data)
		{
			return new CryptedMessage(symmetricCryptSessionId,data);
		}
	}
}