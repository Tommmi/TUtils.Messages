using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TUtils.Common.Extensions;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Common.Logging.LogMocs;
using TUtils.Messages.Common.Net;
using TUtils.Messages.Core.Net;

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class NetClientTest
	{
		[TestMethod]
		public async Task TestNetClient1()
		{
			var logImpl = new LogConsoleWriter(
				LogSeverityEnum.ERROR,
				namespacesWhiteList: new List<string> { "*" },
				namespacesBlackList: new List<string>());
			var logger = new TLog(logImpl, isLoggingOfMethodNameActivated: false);
			var cancellationSource = new CancellationTokenSource();
			var cancellationToken = cancellationSource.Token;
			var netClient = new NetHttpClient(
				baseUri: new Uri("http://localhost:8097"),
				thisClientNodeAddress: new NetNodeAddress("client"),
				additionalConfiguration: null,
				cancellationToken: cancellationToken,
				logger: logger) as INetClient;
#pragma warning disable 4014
			StartServerTask(cancellationToken).LogExceptions(logger);
#pragma warning restore 4014
			var res = await netClient.Enqueue(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 });
			Assert.IsTrue(res == NetActionResultEnum.Succeeded);
		}

		private async Task StartServerTask(CancellationToken cancellationToken)
		{
			using (var httpListener = new HttpListener())
			{
				httpListener.Prefixes.Add("http://localhost:8097/");
				httpListener.Start();

				cancellationToken.ThrowIfCancellationRequested();
				var context = await httpListener.GetContextAsync();
				var request = context.Request;

				// ReSharper disable once UnusedVariable
				using (var response = context.Response)
				{
					var clientAddress = request.Headers.GetValues(NetHttpClient.NetNodeAddressHttpHeaderValueName)?.First();
					Assert.IsTrue(clientAddress == "client");
					Assert.IsTrue(request.RemoteEndPoint != null);
					var ipOfClient = request.RemoteEndPoint.Address;
					Assert.IsTrue(ipOfClient.Equals(IPAddress.Parse("::1")));
					Assert.IsTrue(request.RawUrl.EndsWith("enqueue"));

					using (var memStream = new MemoryStream())
					{
						request.InputStream.CopyTo(memStream);
						var bytes = memStream.ToArray();
						Assert.IsTrue(bytes.AreEquals(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 }));
					}
				}
			}

		}

		[TestMethod]
		public async Task TestNetClient2()
		{
			var logWriter = new LogConsoleWriter(
				LogSeverityEnum.ERROR,
				namespacesWhiteList: new List<string> { "*" },
				namespacesBlackList: new List<string>());
			var rootAssembly = Assembly.GetAssembly(GetType());
			var envServer = new ServerStandardEnvironment(logWriter, timeoutForLongPollingRequest:2000, rootAssemblies:rootAssembly);
			var envClient = new ClientStandardEnvironment(
				logWriter,
				clientUri: "Thomas",
				additionalConfiguration: null,
				diconnectedRetryIntervallTimeMs: 30 * 1000,
				rootAssemblies: rootAssembly);

			try
			{
				var httpServerTask = new SimpleHttpServer(
					envServer.NetServer,
					envServer.CancellationToken,
					envServer.Logger,
					listenPort:8097);
				httpServerTask.Start();

				envServer.BusStop
					.On<TestRequestMessage>()
					.Do((message, token) =>
					{
						envServer.BusStop.Post(new TestResponseMessage(message));
						return Task.CompletedTask;
					});

				await envClient.ConnectToServer(new Uri("http://localhost:8097"));
				var result = await envClient.BusStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));

				Assert.IsTrue(!result.TimeoutElapsed);
				Assert.IsTrue(result.Value.Value == "hello world");

				envServer.CancelSource.Cancel();
				await httpServerTask.WaitForTermination();
			}
			catch (Exception e)
			{
				envServer.Logger.LogException(e);
				Assert.IsTrue(
					envServer.CancellationToken.IsCancellationRequested
					|| envClient.CancellationToken.IsCancellationRequested);

			}
		}

	}
}
