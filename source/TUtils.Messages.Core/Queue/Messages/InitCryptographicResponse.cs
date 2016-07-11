using System;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Symmetric.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	public class InitCryptographicResponse : IInitCryptographicResponse
	{
		private readonly Guid _assymetricCryptSessionId;
		private readonly Guid _symmetricCryptSessionId;
		private readonly byte[] _encryptedSymmetricSecret;
		private readonly IPublicCertContentBase64String _clientCertifikate;
		private readonly byte[] _signature;

		public InitCryptographicResponse(
			Guid assymetricCryptSessionId, 
			Guid symmetricCryptSessionId,
			byte[] encryptedSymmetricSecret, 
			IPublicCertContentBase64String clientCertifikate, 
			byte[] signature)
		{
			_assymetricCryptSessionId = assymetricCryptSessionId;
			_symmetricCryptSessionId = symmetricCryptSessionId;
			_encryptedSymmetricSecret = encryptedSymmetricSecret;
			_clientCertifikate = clientCertifikate;
			_signature = signature;
		}

		Guid IInitCryptographicResponse.AssymetricCryptSessionId => _assymetricCryptSessionId;

		Guid IInitCryptographicResponse.SymmetricCryptSessionId => _symmetricCryptSessionId;

		/// <summary>
		/// SymmetricSecret encrypted
		/// </summary>
		byte[] IInitCryptographicResponse.EncryptedSymmetricSecret => _encryptedSymmetricSecret;

		IPublicCertContentBase64String IInitCryptographicResponse.ClientCertifikate => _clientCertifikate;

		byte[] IInitCryptographicResponse.Signature => _signature;
	}
}