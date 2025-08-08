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
			int port = Random.Shared.Next(minValue: 8099, maxValue: 9400);

			var cancellationSource = new CancellationTokenSource();
			var cancellationToken = cancellationSource.Token;
			var netClient = new NetHttpClient(
				baseUri: new Uri($"http://localhost:{port}"),
				thisClientNodeAddress: new NetNodeAddress("client"),
				additionalConfiguration: null,
				cancellationToken: cancellationToken
				) as INetClient;
#pragma warning disable 4014
			StartServerTask(cancellationToken,port).LogExceptions();
#pragma warning restore 4014
			var res = await netClient.Enqueue(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 0});
			Assert.IsTrue(res == NetActionResultEnum.Succeeded);
		}

		private async Task StartServerTask(CancellationToken cancellationToken, int port)
		{
			using (var httpListener = new HttpListener())
			{
				httpListener.Prefixes.Add($"http://localhost:{port}/");
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
						Assert.IsTrue(bytes.AreEquals(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 0}));
					}
				}
			}

		}

        [TestInitialize]
        public void Initialize()
        {
            this.InitializeConsoleLogging(LogSeverityEnum.INFO);
        }

		[TestMethod]
		public async Task TestNetClient2()
		{
			var rootAssembly = Assembly.GetAssembly(GetType());
			var envServer = new ServerStandardEnvironment(timeoutForLongPollingRequest: 2000,
				rootAssemblies: rootAssembly);
			var envClient = new ClientStandardEnvironment(
				clientUri: "Thomas",
				additionalConfiguration: null,
				diconnectedRetryIntervallTimeMs: 30 * 1000,
				rootAssemblies: rootAssembly);

			try
			{
				int port = Random.Shared.Next(minValue: 8099, maxValue: 9400);

				using var httpServerTask = new SimpleHttpServer(
					envServer.NetServer,
					envServer.CancellationToken,
					listenPort: port);
				bool started = await httpServerTask.StartAndWait(TimeSpan.FromSeconds(10));
				Assert.IsTrue(started);

				envServer.BusStop
					.On<TestRequestMessage>()
					.Do((message, token) =>
					{
						envServer.BusStop.Post(new TestResponseMessage(message));
						return Task.CompletedTask;
					});

				await envClient.ConnectToServer(new Uri($"http://localhost:{port}"));
				var result =
					await envClient.BusStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
						new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));

				Assert.IsTrue(!result.TimeoutElapsed);
				Assert.IsTrue(result.Value.Value == "hello world");

				envServer.CancelSource.Cancel();
				await httpServerTask.WaitForTermination();
			}
			catch (Exception e)
			{
				this.Log().LogError(e: e);
				Assert.IsTrue(
					envServer.CancellationToken.IsCancellationRequested
					|| envClient.CancellationToken.IsCancellationRequested);

			}
		}

		/// <summary>
		/// long polling test
		/// </summary>
		/// <returns></returns>
		[TestMethod]
		public async Task TestLongPolling()
		{
			var rootAssembly = Assembly.GetAssembly(GetType());
			var envServer = new ServerStandardEnvironment(timeoutForLongPollingRequest: 2*60*1000,
				rootAssemblies: rootAssembly);
			var envClient = new ClientStandardEnvironment(
				clientUri: "Thomas",
				additionalConfiguration: null,
				diconnectedRetryIntervallTimeMs: 30 * 1000,
				rootAssemblies: rootAssembly);

			try
			{
				int port = Random.Shared.Next(minValue: 8099, maxValue: 9400);
				var httpServerTask = new SimpleHttpServer(
					envServer.NetServer,
					envServer.CancellationToken,
					listenPort: port);
				bool started = await httpServerTask.StartAndWait(TimeSpan.FromSeconds(10));
				Assert.IsTrue(started);

				int u = 0;

				// server responds to message TestRequestMessage immediately with message TestResponseMessage,
				// waits one minute and sends message Message2Client to the client.
				envServer.BusStop
					.On<TestRequestMessage>()
					.Do(async (message, token) =>
					{
						envServer.BusStop.Post(new TestResponseMessage(message));
						await Task.Delay(1 * 60 * 1000);
						envServer.BusStop.Post(new Message2Client(envClient.BusStop.BusStopAddress, (1234+u++).ToString()));
					});

				await envClient.ConnectToServer(new Uri($"http://localhost:{port}"));

				// send TestRequestMessage to the server
				var result =
					await envClient.BusStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
						new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));

				// when server answered with TestResponseMessage

				Assert.IsTrue(!result.TimeoutElapsed);
				Assert.IsTrue(result.Value.Value == "hello world");

				// waiting for Message2Client
				var res = await envClient.BusStop.WaitOnMessageToMe<Message2Client>(timeoutMs: 15 * 6 * 1000);

				Assert.IsTrue(!res.TimeoutElapsed);
				Assert.IsTrue(res.Value.Value == "1234");

				// send TestRequestMessage to the server
				envClient.BusStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
						new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));

				// waiting for Message2Client, but only 24 seconds
				res = await envClient.BusStop.WaitOnMessageToMe<Message2Client>(timeoutMs: 4 * 6 * 1000);
				Assert.IsTrue(res.TimeoutElapsed);

				// waiting for Message2Client for a long time
				res = await envClient.BusStop.WaitOnMessageToMe<Message2Client>(timeoutMs: 15 * 6 * 1000);

				Assert.IsTrue(!res.TimeoutElapsed);
				Assert.IsTrue(res.Value.Value == "1235");

				envServer.CancelSource.Cancel();
				await httpServerTask.WaitForTermination();
			}
			catch (Exception e)
			{
				this.Log().LogError(e: e);
				Assert.IsTrue(
					envServer.CancellationToken.IsCancellationRequested
					|| envClient.CancellationToken.IsCancellationRequested);

			}
		}
	}
}
