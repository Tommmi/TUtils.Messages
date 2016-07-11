using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TUtils.Common.Security.Asymmetric.Common;
using TUtils.Messages.Common.Queue.messages;

namespace TUtils.Messages.Core.Queue.Messages
{
	public class InitCryptographic : IInitCryptographic
	{
		private readonly Guid _assymetricCryptSessionId;
		private readonly IPublicCertContentBase64String _publicCertifikate;

		public InitCryptographic(Guid assymetricCryptSessionId, IPublicCertContentBase64String publicCertifikate)
		{
			_assymetricCryptSessionId = assymetricCryptSessionId;
			_publicCertifikate = publicCertifikate;
		}

		Guid IInitCryptographic.AssymetricCryptSessionId => _assymetricCryptSessionId;

		IPublicCertContentBase64String IInitCryptographic.PublicCertifikate => _publicCertifikate;
	}
}
