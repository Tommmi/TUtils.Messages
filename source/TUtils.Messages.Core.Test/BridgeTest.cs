using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.LogMocs;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Core.Queue;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Local

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class BridgeTest
	{
		private class SimpleRequestMessage : IRequestMessage
		{
			public string Value { get; }
			public IAddress Destination { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }

			public SimpleRequestMessage(IAddress destination, string value)
			{
				Value = value;
				Destination = destination;
			}
		}

		private class SimpleResponseMessage : IResponseMessage
		{
			public string Value { get; }
			public IAddress Destination { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }

			public SimpleResponseMessage(SimpleRequestMessage msg, string value)
			{
				Value = value;
				Destination = msg.Source;
				RequestId = msg.RequestId;
			}
		}


		private class Env
		{
			public CancellationTokenSource CancellationTokenSource { get; }
			public CancellationToken CancellationToken { get; }
			public List<IMessageBus> Busses { get; }
			public Bridge Bridge { get; }
			public Dictionary<IMessageBus, List<IBusStop>> Clients { get; }
			public UniqueTimeStampCreator UniqueTimeStampCreator { get; }
			public Dictionary<IMessageBus, IBusStopFactory> ClientFactory { get; }

			public Env(int maxCountRunningTasks)
			{
				CancellationTokenSource = new CancellationTokenSource();
				CancellationToken = CancellationTokenSource.Token;
				TLog logger = new TLog(new LogMocWriter(), false);
				var queueFactory = new InprocessQueueFactory(CancellationToken);
				UniqueTimeStampCreator = new UniqueTimeStampCreator();
				var time = new SystemTimeProvider();
				Busses = new List<IMessageBus>();
				for (int i = 0; i < 3; i++)
				{
					Busses.Add(new MessageBus(
						$"local bus {i}", 
						queueFactory, 
						CancellationToken, 
						UniqueTimeStampCreator, 
						maxCountRunningTasks,
						logger));
				}

				Bridge = new Bridge(logger);

				var addressgenerator = new AddressGenerator();
				ClientFactory = new Dictionary<IMessageBus, IBusStopFactory>();

				foreach (var bus in Busses)
				{
					ClientFactory[bus] = new BusStopFactory(
						bus,
						UniqueTimeStampCreator,
						queueFactory, 
						addressgenerator,
						CancellationToken,
						time,
						defaultTimeoutMs:20000);
				}
				Clients = new Dictionary<IMessageBus,List<IBusStop>>();
				foreach (var bus in Busses)
				{
					Clients[bus] = new List<IBusStop>();
					var clientFactory = ClientFactory[bus];
					for (int i = 0; i < 3; i++)
					{
						var busName = bus.GetBusName().WaitAndGetResult(CancellationToken);
						Clients[bus].Add(clientFactory.Create($"client {i} of bus {busName}").WaitAndGetResult(CancellationToken));
					}
				}
			}

		}

		[TestMethod]
		public async Task TestBridge1()
		{
			var env = new Env(maxCountRunningTasks: 1);
			foreach (var bus in env.Busses)
			{
				await env.Bridge.AddBus(bus);
			}

			var client0_0 = env.Clients[env.Busses[0]][0];
			var client0_1 = env.Clients[env.Busses[0]][1];
			var client1_0 = env.Clients[env.Busses[1]][0];

			client0_1
				.On<SimpleRequestMessage>()
				.IncludingBroadcastMessages()
				.Do((message, cancellationToken) =>
				{
					var respMsg = new SimpleResponseMessage(message, "not allowed");
					respMsg.RequestId = 7328;
					client0_1.Post(respMsg);
					return Task.CompletedTask;
				});
			client0_0
				.On<SimpleRequestMessage>()
				.Do((message, cancellationToken) =>
				{
					client0_0.Post(new SimpleResponseMessage(message, message.Value));
					return Task.CompletedTask;
				});

			// von client 0 bus 1 SimpleRequestMessage an client 0 bus 0 senden und auf Antwort warten
			var responseMsg = await client1_0.Send<SimpleRequestMessage, SimpleResponseMessage>(
				new SimpleRequestMessage(client0_0.BusStopAddress, "hello world"));
			Assert.IsTrue(responseMsg.Value == "hello world");

			env.Bridge.RemoveBus(env.Busses.First());
			env.Bridge.Deactivate();
		}

		[TestMethod]
		public async Task TestBridge2()
		{
			var env = new Env(maxCountRunningTasks: 1);

			var client0_0 = env.Clients[env.Busses[0]][0];
			var client0_1 = env.Clients[env.Busses[0]][1];
			var client1_0 = env.Clients[env.Busses[1]][0];

			client0_1
				.On<SimpleRequestMessage>()
				.IncludingBroadcastMessages()
				.Do((message, cancellationToken) =>
				{
					var respMsg = new SimpleResponseMessage(message, "not allowed");
					respMsg.RequestId = 7328;
					client0_1.Post(respMsg);
					return Task.CompletedTask;
				});
			client0_0
				.On<SimpleRequestMessage>()
				.Do((message, cancellationToken) =>
				{
					client0_0.Post(new SimpleResponseMessage(message, message.Value));
					return Task.CompletedTask;
				});

			foreach (var bus in env.Busses)
			{
				await env.Bridge.AddBus(bus);
			}

			var responseMsg = await client1_0.Send<SimpleRequestMessage, SimpleResponseMessage>(
				new SimpleRequestMessage(client0_0.BusStopAddress, "hello world"));
			Assert.IsTrue(responseMsg.Value == "hello world");

			env.Bridge.RemoveBus(env.Busses.First());
			env.Bridge.Deactivate();
		}

		[TestMethod]
		public async Task TestBridge3()
		{
			var env = new Env(maxCountRunningTasks: 1);
			foreach (var bus in env.Busses)
			{
				await env.Bridge.AddBus(bus);
			}

			var client0_4 = await env.ClientFactory[env.Busses[0]].Create("node 0_4");
			var client0_5 = await env.ClientFactory[env.Busses[0]].Create("node 0_5");
			var client1_4 = await env.ClientFactory[env.Busses[1]].Create("node 1_4");

			client0_5
				.On<SimpleRequestMessage>()
				.IncludingBroadcastMessages()
				.Do((message, cancellationToken) =>
				{
					var respMsg = new SimpleResponseMessage(message, "not allowed");
					respMsg.RequestId = 7328;
					client0_5.Post(respMsg);
					return Task.CompletedTask;
				});
			client0_4
				.On<SimpleRequestMessage>()
				.Do((message, cancellationToken) =>
				{
					client0_4.Post(new SimpleResponseMessage(message, message.Value));
					return Task.CompletedTask;
				});

			// von client 4 bus 1 SimpleRequestMessage an client 4 bus 0 senden und auf Antwort warten
			var responseMsg = await client1_4.Send<SimpleRequestMessage, SimpleResponseMessage>(
				new SimpleRequestMessage(client0_4.BusStopAddress, "hello world"));
			Assert.IsTrue(responseMsg.Value == "hello world");

			env.Bridge.RemoveBus(env.Busses.First());
			env.Bridge.Deactivate();
		}

	}
}
