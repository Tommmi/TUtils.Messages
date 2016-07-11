using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bridge;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Common.Queue.messages;
using TUtils.Messages.Core.Bridges;
using TUtils.Messages.Core.Bus;
using TUtils.Messages.Core.Net;
using TUtils.Messages.Core.Queue;
using TUtils.Messages.Core.Queue.Common;
using TUtils.Messages.Core.Queue.Messages;
using TUtils.Messages.Core.Serializer;
// ReSharper disable MemberCanBePrivate.Global

namespace TUtils.Messages.Core
{
	public class ServerStandardEnvironment
	{
		public IBusStop BusStop { get; }
		public IMessageBus Bus { get; }
		public TLog Logger { get; }

		public INetServer NetServer { get; }

		public InprocessQueueFactory QueueFactory { get; }
		public CancellationToken CancellationToken { get; }
		public CancellationTokenSource CancelSource { get; }
		public IMessageSerializer Serializer { get; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="logImplementor"></param>
		/// <param name="rootAssemblies">
		/// assemblies, which contains serializable types.
		/// Referenced assemblies will be included automatically.
		/// If null the default is Assembly.GetEntry()
		/// </param>
		public ServerStandardEnvironment(
			ILogWriter logImplementor,
			params Assembly[] rootAssemblies)
		{
			CancelSource = new CancellationTokenSource();
			CancellationToken = CancelSource.Token;
			QueueFactory = new InprocessQueueFactory(CancellationToken);
			var uniqueTimeStampCreator = new UniqueTimeStampCreator();
			Logger = new TLog(logImplementor, isLoggingOfMethodNameActivated: false);
			var systemTime = new SystemTimeProvider();
			var clientLoadBalancing = new ClientLoadBalancing(CancellationToken, Logger, uniqueTimeStampCreator, systemTime, maxCountOfWaitingTasks: 100);
			IQueueEntryProtocol queueEntryProtocol = new QueueEntryProtocol();
			IMessageBusBaseProtocol messageBusBaseProtocol = new MessageBusBaseProtocol();
			IBridgeProtocol bridgeProtocol = new BridgeProtocol();
			Serializer = new MessageSerializer(
				rootAssemblies: rootAssemblies.ToList(),
				blacklistFilter: type => false,
				additionalTypes: null);
			IAddressGenerator addressGenerator = new AddressGenerator();

			// create message bus
			Bus = new MessageBus(
				busName: "local bus",
				queueFactory: QueueFactory,
				cancellationToken: CancellationToken,
				uniqueTimeStampCreator: uniqueTimeStampCreator,
				maxCountRunningTasks: 5,
				logger: Logger);
			var reliableMessageProtocol = new ReliableMessageProtocol(uniqueTimeStampCreator);

			// create 
			NetServer = new NetServer(
				clientLoadBalancing,
				Serializer,
				QueueFactory,
				Bus,
				CancellationToken,
				queueEntryProtocol,
				messageBusBaseProtocol,
				bridgeProtocol,
				Logger,
				getTimeoutForLongPollingRequest: () => 2000);

			var busStopFactory = new BusStopFactory(
				Bus, 
				uniqueTimeStampCreator, 
				QueueFactory, 
				addressGenerator, 
				CancellationToken,
				systemTime,
				defaultTimeoutMs:20000) as IBusStopFactory;

			BusStop = busStopFactory.Create("bus stop of server").WaitAndGetResult(CancellationToken);
		}
	}
}
