using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
		private CancellationTokenRegistration? _cancellationRegistration;
		private HttpListener _httpListener;
		private Task _serverTask;
		
		private object _sync = new ();
		private enum Status
		{
			Stopped,
			Starting,
			Running,
			Stopping
		}
		private Status _status = Status.Stopped;

		public SimpleHttpServer(
			INetServer netServer,
			CancellationToken cancellationToken,
			int listenPort
			)
		{
			_netServer = netServer;
			_listenPort = listenPort;
			_cancellationToken = cancellationToken;
		}

		public async Task<bool> StartAndWait(TimeSpan timeout)
		{
			Start();
			var elapsed = DateTime.UtcNow + timeout;
			while(DateTime.UtcNow < elapsed)
			{
				lock (_sync)
				{
					if(_status == Status.Running)
					{
						return true;
					}
				}

				await Task.Delay(50);
			}

			return false;
		}


		// ReSharper disable once UnusedMember.Global
		public void Start()
		{
			lock (_sync)
			{
				switch(_status)
				{
					case Status.Stopped:
						_status = Status.Starting;
						break;
					case Status.Starting:
					case Status.Running:
						return;
					case Status.Stopping:
						throw new ApplicationException("can't start a stopping service");
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			_serverTask = ServerThread(_cancellationToken, _netServer).LogExceptions();
		}

		public Task WaitForTermination()
		{
			return _serverTask ?? Task.CompletedTask;
		}

		public void Stop()
		{
			lock (_sync)
			{
				switch(_status)
				{
					case Status.Stopped:
						return;
					case Status.Stopping:
						break;
					case Status.Running:
					case Status.Starting:
						_status = Status.Stopping;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}


			if (_httpListener?.IsListening ?? false)
			{
				_httpListener.Stop();
				_httpListener.Close();
			}
			_httpListener = null;
		}

		private async Task ServerThread(CancellationToken cancellationToken, INetServer netServer)
		{
			cancellationToken.ThrowIfCancellationRequested();
			

			foreach (var serverDomain in new[] { "*", "localhost" })
			{
				bool mayTryAgain = false;

				using (_httpListener = new HttpListener())
				{
					_cancellationRegistration = cancellationToken.Register(Stop);

					try
					{
						_httpListener.Prefixes.Add($"http://{serverDomain}:{_listenPort}/");
						_httpListener.Start();

						lock (_sync)
						{
							switch(_status)
							{
								case Status.Starting:
									_status = Status.Running;
									break;
								case Status.Stopping:
									return;
								default:
									throw new ArgumentOutOfRangeException($"{_status}");
							}
						}

						while (true)
						{
							lock(_sync)
							{
								if(_status == Status.Stopping)
								{
									return;
								}
							}

							cancellationToken.ThrowIfCancellationRequested();

							var context = await _httpListener.GetContextAsync();
#pragma warning disable 4014
							HandleRequest(netServer, context).LogExceptions();
#pragma warning restore 4014
						}
					}
					catch (HttpListenerException e) when (e.NativeErrorCode == 5 && serverDomain == "*")
					{
						mayTryAgain = true;
						this.Log().LogWarn(() => new
						{
							desc = "You must run the program with admin rights if you want to receive requests from other computers than this one!"
						});
					}
					catch (HttpListenerException e)
					{
						bool error = false;
						lock(_sync)
						{
							error = _status != Status.Stopping;
						}

						if(error)
						{
							this.Log().LogError(e: e);
						}
					}
					catch (Exception e)
					{
						if (cancellationToken.IsCancellationRequested)
						{
							this.Log().LogInfo(() => new { descr = "server task canceled (source code tag j92hflssaej)" });
							return;
						}
                        this.Log().LogError(e: e);
						throw;
					}
					finally
					{
						if (!mayTryAgain)
						{
							_status = Status.Stopped;
							this.Log().LogWarn(() => new { _listenPort, descr = $"simple server stopped on port {_listenPort}" });
						}
					}

					if(!mayTryAgain)
					{
						return;
					}
				}
			}
		}

		private static async Task HandleRequest(INetServer netServer, HttpListenerContext context)
		{
			using var response = context.Response;
			var request = context.Request;

			// Header sicher lesen
			if (!TryGetRequiredHeader(request, NetHttpClient.NetNodeAddressHttpHeaderValueName, out var rawHeader))
			{
				SendError(response, HttpStatusCode.BadRequest,
					$"Missing required header: {NetHttpClient.NetNodeAddressHttpHeaderValueName}");
				return;
			}

			// Header-Inhalt validieren (falls es einen TryParse o.ä. gibt – bevorzugt statt ctor-Exception)
			if (!TryParse(rawHeader,out var clientaddress))
			{
				SendError(response, HttpStatusCode.BadRequest,
					$"Invalid header {NetHttpClient.NetNodeAddressHttpHeaderValueName}: '{rawHeader}'");
				return;
			}

			if (request.RemoteEndPoint == null)
			{
				SendBadRequest(response, "no remote endpoint");
				return;
			}

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
							await request.InputStream.CopyToAsync(memStream);
							return new ByteMessageContent(memStream.ToArray());
						}
					});
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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
						break;
					case ResponseEnum.AuthenticationFailed:
						response.StatusCode = (int)HttpStatusCode.Forbidden;
						break;
					case ResponseEnum.Timeout:
						response.StatusCode = (int)HttpStatusCode.RequestTimeout;
						break;
					default:
						// ReSharper disable once NotResolvedInText
						throw new ArgumentOutOfRangeException("6736543hdh234h");
				}
			}
			else
			{
				SendBadRequest(response, "Missing X-Custom header");
			}
		}

		public static bool TryParse(string? s, out NetNodeAddress? address)
		{
			address = null;
			if (string.IsNullOrWhiteSpace(s))
				return false;

			try
			{
				address = new NetNodeAddress(s);
				return true;
			}
			catch
			{
				return false;
			}
		}



		// 1) Kleine Helfer
		private static bool TryGetRequiredHeader(HttpListenerRequest request, string name, out string value)
		{
			// Indexer ist ok; gibt null zurück, wenn der Header fehlt
			value = request.Headers[name];

			if (!string.IsNullOrWhiteSpace(value))
				return true;

			// Falls du mehrere gleiche Header erwartest:
			var vals = request.Headers.GetValues(name);
			value = vals?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

			return !string.IsNullOrWhiteSpace(value);
		}

		private static void SendError(HttpListenerResponse resp, HttpStatusCode code, string message)
		{
			resp.StatusCode = (int)code;
			resp.ContentType = "text/plain; charset=utf-8";
			using var w = new StreamWriter(resp.OutputStream, Encoding.UTF8, leaveOpen: false);
			w.Write(message ?? code.ToString());
			// using auf resp schließt unten alles
		}


		private static void SendBadRequest(HttpListenerResponse response, string message)
		{
			response.StatusCode = (int)HttpStatusCode.BadRequest; // 400
			response.ContentType = "text/plain";

			using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8);
			writer.Write(message ?? "Bad Request");
		}

		public void Dispose()
		{
			Stop();
			_cancellationRegistration?.Dispose();
			_cancellationRegistration = null;
		}
	}
}
