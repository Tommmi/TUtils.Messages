using System;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Common.Security.Symmetric.Common;

namespace TUtils.Messages.Common.Queue.messages
{
	public interface IInitCryptographicResponse
	{
		Guid AssymetricCryptSessionId { get; }
		Guid SymmetricCryptSessionId { get; }
		/// <summary>
		/// SymmetricSecret encrypted
		/// </summary>
		byte[] EncryptedSymmetricSecret { get; }
		/// <summary>
		/// public certificate of sender of this response message
		/// </summary>
		IPublicCertContentBase64String ClientCertifikate { get; }
		/// <summary>
		/// signature of EncryptedSymmetricSecret.Data
		/// </summary>
		byte[] Signature { get; }
	}
}