using System;
using TUtils.Common.Security.Asymmetric.Common;

namespace TUtils.Messages.Common.Queue.messages
{
	public interface IInitCryptographic
	{
		Guid AssymetricCryptSessionId { get; }
		IPublicCertContentBase64String PublicCertifikate { get; }
	}
}