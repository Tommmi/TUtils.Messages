using System;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Symmetric.Common;

namespace TUtils.Messages.Common.Queue.messages
{
	public interface ICryptProtocol
	{
		IInitCryptographic CreateInitCryptographic(Guid assymetricCryptSessionId, IPublicCertContentBase64String publicCertifikate);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="request"></param>
		/// <param name="symmetricCryptSessionId"></param>
		/// <param name="encryptedSymmetricSecret">
		/// SymmetricSecret
		/// </param>
		/// <param name="clientCertifikate">
		/// public certificate of sender of this response message
		/// </param>
		/// <param name="signature">
		/// signature of EncryptedSymmetricSecret.
		/// </param>
		/// <returns></returns>
		IInitCryptographicResponse CreateInitCryptographicResponse(
			IInitCryptographic request, 
			Guid symmetricCryptSessionId,
			byte[] encryptedSymmetricSecret,
			IPublicCertContentBase64String clientCertifikate,
			byte[] signature);
		ICryptedMessage CreateEncryptedMessage(Guid symmetricCryptSessionId, EncryptedData data);
	}
}