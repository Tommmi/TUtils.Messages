using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Core.Net;
// ReSharper disable MemberCanBePrivate.Global

namespace TUtils.Messages.Core
{
	// ReSharper disable once UnusedMember.Global
	public class SimpleHttpServer : IDisposable
	{
		private readonly INetServer _netServer;
		private readonly int _listenPort;
		private readonly CancellationToken _cancellationToken;
		private readonly ITLog _logger;
		private CancellationTokenRegistration? _cancellationRegistration;
		private HttpListener _httpListener;
		private Task _serverTask;

		public SimpleHttpServer(
			INetServer netServer,
			CancellationToken cancellationToken,
			ITLog logger,
			int listenPort
			)
		{
			_netServer = netServer;
			_listenPort = listenPort;
			_cancellationToken = cancellationToken;
			_logger = logger;
		}

		// ReSharper disable once UnusedMember.Global
		public void Start()
		{
			_serverTask = ServerThread(_cancellationToken, _netServer, _logger).LogExceptions(_logger);
		}

		public Task WaitForTermination()
		{
			return _serverTask ?? Task.CompletedTask;
		}

		public void Stop()
		{
			if (_httpListener?.IsListening ?? false)
			{
				_httpListener.Stop();
				_httpListener.Close();
			}
			_httpListener = null;
		}

		private async Task ServerThread(CancellationToken cancellationToken, INetServer netServer, ITLog logger)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var serverDomain in new[] { "*", "localhost" })
			{
				using (_httpListener = new HttpListener())
				{
					_cancellationRegistration = cancellationToken.Register(Stop);

					try
					{
						_httpListener.Prefixes.Add($"http://{serverDomain}:{_listenPort}/");
						_httpListener.Start();

						while (true)
						{
							cancellationToken.ThrowIfCancellationRequested();

							var context = await _httpListener.GetContextAsync();
#pragma warning disable 4014
							HandleRequest(netServer, context).LogExceptions(logger);
#pragma warning restore 4014
						}
					}
					catch (HttpListenerException e) when (e.Message == "Access is denied" && serverDomain == "*")
					{
						_logger.Log(LogSeverityEnum.WARNING, this,
							"you must run program with admin rights, if you want to receive requests from other computers than this one !");
					}
					catch (HttpListenerException e)
					{
						_logger.LogException(e);
					}
					catch (Exception e)
					{
						if (cancellationToken.IsCancellationRequested)
						{
							_logger.LogInfo(this, "server task canceled (source code tag j92hflssaej)");
							return;
						}
						_logger.LogException(e);
						throw;
					}
				}
			}
		}

		private static async Task HandleRequest(INetServer netServer, HttpListenerContext context)
		{
			var request = context.Request;
			var response = context.Response;
			var clientaddress =
				new NetNodeAddress(request.Headers.GetValues(NetHttpClient.NetNodeAddressHttpHeaderValueName)?.First());
			if (request.RemoteEndPoint == null)
				return;
			var ipOfClient = request.RemoteEndPoint.Address;
			if (request.RawUrl.EndsWith("enqueue"))
			{
				await netServer.OnEnqueue(
					source: clientaddress,
					ipAddress: ipOfClient,
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
					getContent: async () =>
					{
						using (var memStream = new MemoryStream())
						{
							request.InputStream.CopyTo(memStream);
							return new ByteMessageContent(memStream.ToArray());
						}
					});
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
				response.OutputStream.Close();
			}
			else if (request.RawUrl.EndsWith("dequeue"))
			{
				var res = await netServer.OnDequeue(
					clientaddress,
					ipOfClient);
				switch (res.Result)
				{
					case ResponseEnum.Succeeded:
						var data = res.Content.GetData();
						response.ContentType = "application/octet-stream";
						response.ContentLength64 = data.LongLength;
						response.OutputStream.Write(data, 0, data.Length);
						response.OutputStream.Close();
						break;
					case ResponseEnum.AuthenticationFailed:
						response.StatusCode = (int)HttpStatusCode.Forbidden;
						response.OutputStream.Close();
						break;
					case ResponseEnum.Timeout:
						response.StatusCode = (int)HttpStatusCode.RequestTimeout;
						response.OutputStream.Close();
						break;
					default:
						// ReSharper disable once NotResolvedInText
						throw new ArgumentOutOfRangeException("6736543hdh234h");
				}
			}
		}


		public void Dispose()
		{
			_cancellationRegistration?.Dispose();
			_cancellationRegistration = null;
		}
	}
}
