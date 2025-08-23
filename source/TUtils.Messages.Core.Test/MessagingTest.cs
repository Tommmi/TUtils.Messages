using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Async;
using TUtils.Common.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Common.Logging.LogMocs;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Messages;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Common;
using TUtils.Messages.Core.Queue;
#pragma warning disable 4014

namespace TUtils.Messages.Core.Test
{
	[TestClass]
	public class MessagingTest
	{
		#region types

		#region messages

		private class MyPrioMessage : IPrioMessage
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public byte Priority { get; set; }
		}

		private class MyRequestMessage : IPrioMessage, IRequestMessage
		{
			public MyRequestMessage(byte priority, IAddress destination)
			{
				Priority = priority;
				Destination = destination;
			}

			public byte Priority { get; }
			public IAddress Destination { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }
		}

		private class MyResponseMessage : IPrioMessage, IResponseMessage
		{
			public MyResponseMessage(byte priority, IAddress destination, long requestId)
			{
				Priority = priority;
				Destination = destination;
				RequestId = requestId;
			}

			public MyResponseMessage(byte priority, MyRequestMessage requestMessage)
				: this(priority, requestMessage.Source, requestMessage.RequestId)
			{
			}

			public byte Priority { get; }
			public IAddress Destination { get; }
			public IAddress Source { get; set; }
			public long RequestId { get; set; }
		}

		private class HelloBroadcastMessage 
		{
			// ReSharper disable once UnusedAutoPropertyAccessor.Local
			public IAddress MyAddress { get; }
			public string MyFeatures { get; }

			public HelloBroadcastMessage(IAddress myAddress, string myFeatures)
			{
				MyAddress = myAddress;
				MyFeatures = myFeatures;
			}
		}

		#endregion

		private class InprocessBusEnvironment
		{
			public readonly CancellationTokenSource CancellationSource;
			public readonly CancellationToken CancellationToken;
			public readonly IQueueFactory QueueFactory;
			public readonly MessageBus MessageBus;
			public readonly IMessageBus Bus;
			public readonly IBusStopFactory StopFactory;
			public readonly IBusStop BusStop1;
			public readonly IBusStop BusStop2;

			public InprocessBusEnvironment()
			{
				var cancellationSource = new CancellationTokenSource();
				var cancellationToken = cancellationSource.Token;
				var queueFactory = new InprocessQueueFactory(cancellationToken);
				var timeStampCreator = new UniqueTimeStampCreator();
				var time = new SystemTimeProvider();
				// var taskScheduler = TaskScheduler.Default;
				var messageBus = new MessageBus("local bus", queueFactory, cancellationToken, timeStampCreator,10);
				var bus = messageBus;
				var addressGenerator = new AddressGenerator();
				var busStopFactory =
					new BusStopFactory(
						bus, 
						timeStampCreator, 
						queueFactory, 
						addressGenerator, 
						cancellationToken,
						time,
						defaultTimeoutMs:20000) as IBusStopFactory;

				var busStop1 = busStopFactory.Create("A").WaitAndGetResult(cancellationToken);
				var busStop2 = busStopFactory.Create("B").WaitAndGetResult(cancellationToken);

				CancellationSource = cancellationSource;
				CancellationToken = cancellationToken;
				QueueFactory = queueFactory;
				MessageBus = messageBus;
				Bus = bus;
				StopFactory = busStopFactory;
				BusStop1 = busStop1;
				BusStop2 = busStop2;
			}
		}

        #endregion

        #region Tests

        [TestInitialize]
        public void Initialize()
        {
            this.InitializeConsoleLogging(LogSeverityEnum.INFO);
        }


        #region SendMessagesStresstest

        [TestMethod]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public async Task SendMessagesStresstest()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			var env = new InprocessBusEnvironment();

			var clients = new List<IBusStop>();
			for (int i = 0; i < 10; i++)
				clients.Add(await env.StopFactory.Create(new Address(i.ToString())));

			foreach (var client in clients)
			{
				client.On<MyRequestMessage>().Do(async (msg, cancellationToken) =>
				{
					await Task.Delay(20);
					client.Post(new MyResponseMessage(100, msg));
				});
			}

			var sendThread = new Thread(()=>SendThread(clients, env));
			sendThread.Name = "sender";
			sendThread.Start();
			
			sendThread.Join();
			env.CancellationSource.Cancel();
		}

		private void SendThread(List<IBusStop> clients, InprocessBusEnvironment env)
		{
			SendThreadAsync(clients).Wait(env.CancellationToken);
		}

		private async Task SendThreadAsync(List<IBusStop> clients)
		{
			foreach (var client in clients)
			{
				foreach (var destClient in clients)
				{
					await client.Send<MyRequestMessage, MyResponseMessage>(new MyRequestMessage(1, destClient.BusStopAddress));
				}
			}
		}

		#endregion

		#region SendMessages

		[TestMethod]
		public async Task SendMessages()
		{
			var env = new InprocessBusEnvironment();

			bool client1FoundMsgA = false;
			bool client1FoundMsgB = false;
			bool client2FoundMsgA = false;
			bool client2FoundMsgB = false;

			AsyncEvent evClient1FoundMsgA = new AsyncEvent(env.CancellationToken);
			AsyncEvent evClient1FoundMsgB = new AsyncEvent(env.CancellationToken);
			AsyncEvent evClient2FoundMsgA = new AsyncEvent(env.CancellationToken);
			AsyncEvent evClient2FoundMsgB = new AsyncEvent(env.CancellationToken);

			var taskClient1FoundMsgA = evClient1FoundMsgA.RegisterForEvent();
			var taskClient1FoundMsgB = evClient1FoundMsgB.RegisterForEvent();
			var taskClient2FoundMsgA = evClient2FoundMsgA.RegisterForEvent();
			var taskClient2FoundMsgB = evClient2FoundMsgB.RegisterForEvent();

			env.BusStop1
				.On<HelloBroadcastMessage>()
				.IncludingMessagesToOtherBusStops()
				.FilteredBy(msg => msg.MyFeatures == "hello ! This is client B")
				.Do(async (msg, cancellationToken) =>
				{
					if (msg.MyFeatures == "hello ! This is client B")
					{
						client1FoundMsgB = true;
						evClient1FoundMsgB.Rise();
					}
					await Task.Yield();
				});

			env.BusStop2
				.On<HelloBroadcastMessage>()
				.IncludingMessagesToOtherBusStops()
				.Do(async (msg, cancellationToken) =>
				{
					if (msg.MyFeatures == "hello ! This is client A")
					{
						client2FoundMsgA = true;
						evClient2FoundMsgA.Rise();
					}
					if (msg.MyFeatures == "hello ! This is client B")
					{
						client2FoundMsgB = true;
						evClient2FoundMsgB.Rise();
					}
					await Task.Yield();
				});

			env.BusStop1.Post(new HelloBroadcastMessage(env.BusStop1.BusStopAddress, "hello ! This is client A"));
			env.BusStop2.Post(new HelloBroadcastMessage(env.BusStop1.BusStopAddress, "hello ! This is client B"));
			
			await taskClient1FoundMsgB;
			await taskClient2FoundMsgA;
			await taskClient2FoundMsgB;

			Assert.IsTrue(!client1FoundMsgA);
			Assert.IsTrue(client1FoundMsgB);
			Assert.IsTrue(client2FoundMsgA);
			Assert.IsTrue(client2FoundMsgB);
		}

		#endregion

		#region Cancellation

		[TestMethod]
		public async Task Cancellation()
		{
			var env = new InprocessBusEnvironment();
			var messageReceived = new AsyncEvent(env.CancellationToken);

			// register message handler
			env.Bus.RegisterBroadcast(msg =>
			{
				messageReceived.Rise();
				return Task.CompletedTask;
			});

			// start wait for message
			var waitingForReceived = messageReceived.RegisterForEvent();

			// send message
			await env.Bus.SendPort.Enqueue(new MyPrioMessage());

			// wait for message
			await waitingForReceived;
			Assert.IsTrue(waitingForReceived.IsCompleted);

			// cancel
			env.CancellationSource.Cancel();
			Assert.IsTrue(!env.MessageBus.IsRunning);
		}

		#endregion

		#region double message

		[TestMethod]
        [Timeout(15000)]
        public async Task DoubleMessage()
		{
			var env = new InprocessBusEnvironment();
			var secondLocalQueue = env.QueueFactory.Create();
			env.Bus.Register<HelloBroadcastMessage>(secondLocalQueue.Entry);
			env.Bus.RegisterBroadcast(secondLocalQueue.Entry);
			env.BusStop1.Post(new HelloBroadcastMessage(new Address("broadcast"), "1"));
			env.BusStop1.Post(new HelloBroadcastMessage(new Address("broadcast"), "2"));
			var msg = await secondLocalQueue.Exit.Dequeue() as HelloBroadcastMessage;
			Assert.IsTrue(msg != null);
			Assert.IsTrue(msg.MyFeatures == "1");
			msg = await secondLocalQueue.Exit.Dequeue() as HelloBroadcastMessage;
			Assert.IsTrue(msg != null);
			Assert.IsTrue(msg.MyFeatures == "2");

		}
		#endregion

		#endregion
	}
}
