using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TUtils.Common.Async;
using TUtils.Common.Logging.Common;
using TUtils.Common.Logging.LogMocs;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Net;

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class ClientLaodBalancingTest
	{
		volatile int _countReceivedRequestsFromClient1;
		volatile int _countReceivedRequestsFromClient2;

		[Serializable]
		private class TestMessage
		{
			public string Value { get; }
			public byte[] Data { get; }
			public List<int> Numbers { get; }

			public TestMessage(string value, byte[] data, List<int> numbers)
			{
				Value = value;
				Data = data;
				Numbers = numbers;
			}
		}

		[TestMethod]
		public async Task TestClientLoadBalancing1()
		{
			var logImpl = new LogConsoleWriter(
				LogSeverityEnum.INFO,
				namespacesWhiteList:new List<string>(), 
				namespacesBlackList:new List<string>());
			var env = new ServerStandardEnvironment(logImpl, Assembly.GetAssembly(GetType()));

			_countReceivedRequestsFromClient1 = 0;
			_countReceivedRequestsFromClient2 = 0;

			env.BusStop
				.On<TestMessage>()
				.IncludingMessagesToOtherBusStops()
				.Do((msg, cancel) =>
				{
					if (msg.Value.EndsWith("1"))
						_countReceivedRequestsFromClient1++;
					if (msg.Value.EndsWith("2"))
						_countReceivedRequestsFromClient2++;

					DoSomeHeavyWork();

					if (_countReceivedRequestsFromClient1 > 200)
					{
						// there are as many requests fom client 1 as from client 2
						Assert.IsTrue((_countReceivedRequestsFromClient2 - _countReceivedRequestsFromClient1) < 20);
						env.CancelSource.Cancel();
					}
					return Task.CompletedTask;
				});

			var serverSimulator = AsyncThreadStarter.Start(
				"simulator",
				env.CancellationToken,
				ThreadPriority.Normal,
				env.Logger,
				synchronousThreadMethod: cancel =>
				{
					while (true)
					{
						cancel.ThrowIfCancellationRequested();
						for (int i = 0; i < 10; i++)
							EnqueueNewTestMessage(env.Serializer, env.NetServer, "http://123.23.45.1");
						EnqueueNewTestMessage(env.Serializer, env.NetServer, "http://123.23.45.2");

						Task.Delay(100, cancel).Wait(env.CancellationToken);
					}
#					pragma warning disable 162
					return true;
#					pragma warning restore 162
					// ReSharper disable once FunctionNeverReturns
				});

			await serverSimulator.WaitForStart();
			await serverSimulator.WaitForTermination();
			env.Logger.LogInfo(this,"end of test");
		}

		private static void EnqueueNewTestMessage(
			IMessageSerializer serializer, 
			INetServer netServer,
			string clientUrl)
		{
			Uri uri;
			Uri.TryCreate(clientUrl, UriKind.Absolute, out uri);
			var address = new NetNodeAddress(uri);
			var ip = IPAddress.Parse(uri.Host);
			var msg = new TestMessage(clientUrl, new byte[] {1, 2, 3, 4}, new List<int> {2, 3, 4, 5});
			var messgaeContent = serializer.Serialize(msg);
			// ReSharper disable once UnusedVariable
			var enqueueResult = netServer.OnEnqueue(
				address,
				ip,
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
				async () => messgaeContent);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		}

		private void DoSomeHeavyWork()
		{
			var elapsedTime = DateTime.Now + new TimeSpan(0, 0, 0, seconds:1);
			while (DateTime.Now < elapsedTime)
			{
				for (int i = 0; i < 10000; i++)
				{
					double a = 7867834.23;
					double b = 72653.97;
					// ReSharper disable once UnusedVariable
					double c = a/b;
				}
			}
		}
	}
}
