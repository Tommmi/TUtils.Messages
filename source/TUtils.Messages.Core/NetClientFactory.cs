using System;
using System.Net.Http;
using System.Threading;
using TUtils.Common.Logging;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Core.Net;

namespace TUtils.Messages.Core
{
	public class NetClientFactory : INetClientFactory
	{
		private readonly string _clientUri;
		private readonly CancellationToken _cancellationToken;
		private readonly ITLog _logger;
		private readonly Action<HttpClient> _additionalConfiguration;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="clientUri"></param>
		/// <param name="cancellationToken"></param>
		/// <param name="logger"></param>
		/// <param name="additionalConfiguration">may be null</param>
		public NetClientFactory(
			string clientUri,
			CancellationToken cancellationToken,
			ITLog logger,
			Action<HttpClient> additionalConfiguration)
		{
			_clientUri = clientUri;
			_cancellationToken = cancellationToken;
			_logger = logger;
			_additionalConfiguration = additionalConfiguration;
		}

		INetClient INetClientFactory.Create(Uri serverAddress)
		{
			return new NetHttpClient(serverAddress, new NetNodeAddress(_clientUri),  _additionalConfiguration, _cancellationToken, _logger);
		}
	}
}