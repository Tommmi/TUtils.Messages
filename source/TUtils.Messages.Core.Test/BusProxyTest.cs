using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TUtils.Common;
using TUtils.Common.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Common.Logging.LogMocs;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Bridges;
using TUtils.Messages.Core.Bus;
using TUtils.Messages.Core.Queue;
using TUtils.Messages.Core.Queue.Common;

// ReSharper disable InconsistentNaming

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class BusProxyTest
	{
		private class TestRequestMessage : IRequestMessage
		{
			public TestRequestMessage(IAddress destination, string value)
			{
				Destination = destination;
				Value = value;
			}

			public IAddress Destination { get; }
			public string Value { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }
		}

		private class TestResponseMessage : IResponseMessage
		{
			public string ResponseValue { get; }
			public IAddress Destination { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }

			public TestResponseMessage(TestRequestMessage request, string responseValue)
			{
				ResponseValue = responseValue;
				Destination = request.Source;
				RequestId = request.RequestId;
			}
		}

		private class Env
		{
			// hold it - don't garbage
			// ReSharper disable once NotAccessedField.Local
			private Bus2QueueAdapter _bus2QueueAdapter;
			public IBusStop[,,] Stops { get; }

			public IMessageBus[,] LocalBusses { get; }

			public Bridge[] Bridges { get; }

			public BusProxy BusProxy { get; }

			public IBusStop FarStop { get; }

			public CancellationTokenSource CancellationTokenSource { get;  }

			public Env()
			{
				var cancellationTokenSource = new CancellationTokenSource();
				var cancellationToken = cancellationTokenSource.Token;
				var queueFactory = new InprocessQueueFactory(cancellationToken);
				var queueToMessageBus = queueFactory.Create();
				var queueToBusProxy = queueFactory.Create();
				var messageBusBaseProtocol = new MessageBusBaseProtocol();
				var timestampCreator = new UniqueTimeStampCreator();
				var time = new SystemTimeProvider();
				var logger = new TLog(
					new LogConsoleWriter(
						LogSeverityEnum.INFO,
						namespacesWhiteList: new List<string> { "*" },
						namespacesBlackList: new List<string>()),
					isLoggingOfMethodNameActivated: false);
				var busProxy = new BusProxy(
					queueToMessageBus.Entry,
					queueToBusProxy.Exit,
					messageBusBaseProtocol,
					timestampCreator,
					cancellationToken,
					logger);

				var messageBus = new MessageBus(
					"bus far away",
					queueFactory,
					cancellationToken,
					timestampCreator,
					10,
					logger);
				
				var queueEntryProtocol = new QueueEntryProtocol() as IQueueEntryProtocol;
				var bridgeProtocol = new BridgeProtocol() as IBridgeProtocol;
				_bus2QueueAdapter = new Bus2QueueAdapter(
					messageBus,
					queueToBusProxy.Entry,
					queueToMessageBus.Exit,
					cancellationToken,
					queueEntryProtocol,
					messageBusBaseProtocol,
					bridgeProtocol,
					logger);
				var addressGenerator = new AddressGenerator() as IAddressGenerator;

				var bridges = new Bridge[2];
				var localBusses = new IMessageBus[2, 2];
				var clients = new IBusStop[2,2,2];

				for (int bridgeNb = 0; bridgeNb < 2; bridgeNb++)
				{
					var bridge = new Bridge(logger);
					bridges[bridgeNb] = bridge;

					for (int busNb = 0; busNb < 2; busNb++)
					{
						var bus = new MessageBus(
							$"local bus: bridge {bridgeNb}, bus {busNb}",
							queueFactory,
							cancellationToken,
							timestampCreator,
							maxCountRunningTasks: 2,
							logger:logger);
						localBusses[bridgeNb, busNb] = bus; 

						for (int clientNb = 0; clientNb < 2; clientNb++)
						{
							clients[bridgeNb,busNb,clientNb] = new BusStop.BusStop().Init(
								bus, 
								addressGenerator.Create($"client: bridge {bridgeNb}, bus {busNb}, client {clientNb}"),
								timestampCreator,
								cancellationToken,
								time,
								defaultTimeoutMs:20000).WaitAndGetResult(cancellationToken);
						}
					}
				}

				var clientFarBus = new BusStop.BusStop().Init(
					messageBus,
					addressGenerator.Create("farClient"),
					timestampCreator,
					cancellationToken,
					time,
					defaultTimeoutMs:20000).WaitAndGetResult(cancellationToken);

				CancellationTokenSource = cancellationTokenSource;
				BusProxy = busProxy;
				Bridges = bridges;
				LocalBusses = localBusses;
				Stops = clients;
				FarStop = clientFarBus;
			}

		}

		[TestMethod]
		public async Task TestBusProxy1()
		{
			var env = new Env();

			var bridge_0 = env.Bridges[0];
			var bus_0_0 = env.LocalBusses[0, 0];
			await bridge_0.AddBus(bus_0_0);
			await bridge_0.AddBus(env.BusProxy);
			var client0_0_0 = env.Stops[0, 0, 0];
			env.FarStop
				.On<TestRequestMessage>()
				.Do((msg, cancellationToken) =>
				{
					env.FarStop.Post(new TestResponseMessage(msg,msg.Value+" world"));
					return Task.CompletedTask;
				});
			var response = await client0_0_0.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
				new TestRequestMessage(env.FarStop.BusStopAddress, "hello"));
			Assert.IsTrue(!response.TimeoutElapsed);
			Assert.IsTrue(response.Value.ResponseValue == "hello world");
		}

		[TestMethod]
		public async Task TestCancellation()
		{
			try
			{
				await EventBasedTask();
			}
			catch (Exception e)
			{
				Assert.IsTrue(e is TaskCanceledException);								
			}
		}

		private Task EventBasedTask()
		{
			var tcs = new TaskCompletionSource<bool>();

			Task.Run(async () =>
			{
				await Task.Delay(500);
				tcs.TrySetCanceled();
			});

			return tcs.Task;
		}



		[TestMethod]
		public async Task TestBusProxy2()
		{
			bool codePassed = false;
			try
			{
				var env = new Env();

				// add local busses to local bridges
				// Add also bus proxy to local bridges
				for (int bridgeNb = 0; bridgeNb < 2; bridgeNb++)
				{
					var bridge = env.Bridges[bridgeNb];
					for (int busNb = 0; busNb < 2; busNb++)
					{
						var bus = env.LocalBusses[bridgeNb, busNb];
						await bridge.AddBus(bus);
					}
					await bridge.AddBus(env.BusProxy);
				}

				var client0_0_0 = env.Stops[0, 0, 0];
				var client1_1_1 = env.Stops[1, 1, 1];

				client1_1_1
					.On<TestRequestMessage>()
					.Do((msg, cancellationToken) =>
					{
						env.FarStop.Post(new TestResponseMessage(msg, msg.Value + " world"));
						return Task.CompletedTask;
					});
			
				var response = await client0_0_0.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
					new TestRequestMessage(client1_1_1.BusStopAddress, "hello"));
				Assert.IsTrue(!response.TimeoutElapsed);
				Assert.IsTrue(response.Value.ResponseValue == "hello world");
				codePassed = true;

				env.CancellationTokenSource.Cancel();
			}
			catch (Exception e)
			{
				Assert.IsTrue(e is TaskCanceledException);
			}

			Assert.IsTrue(codePassed);
		}
	}
}
