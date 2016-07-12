using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Queue;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace TUtils.Messages.Core
{
	public class LocalBusEnvironment
	{
		public IBusStop BusStop { get; }
		public CancellationTokenSource CancelSource { get; }
		public ITLog Logger { get; }


		public IMessageBus Bus { get; }
		public IBusStopFactory BusStopFactory { get; }
		public ISystemTimeProvider SystemTime { get; }
		public IAddressGenerator AddressGenerator { get; }
		public IQueueFactory InprocessQueueFactory { get; }
		public IUniqueTimeStampCreator UniqueTimeStampCreator { get; }

		public LocalBusEnvironment(
			ILogWriter logImplementor)
		{
			var cancelSource = new CancellationTokenSource();
			var cancellationToken = cancelSource.Token;
			var queueFactory = new InprocessQueueFactory(cancellationToken);
			var uniqueTimeStampCreator = new UniqueTimeStampCreator();
			var logger = new TLog(logImplementor, isLoggingOfMethodNameActivated: false);

			// create message bus
			var bus = new MessageBus(
				busName: "local bus",
				queueFactory: queueFactory,
				cancellationToken: cancellationToken,
				uniqueTimeStampCreator: uniqueTimeStampCreator,
				maxCountRunningTasks: 5,
				logger: logger);

			var addressGenerator = new AddressGenerator();
			var systemTime = new SystemTimeProvider();
			var busStopFactory = new BusStopFactory(
				bus,
				uniqueTimeStampCreator,
				queueFactory,
				addressGenerator,
				cancellationToken,
				systemTime,
				defaultTimeoutMs: 20000) as IBusStopFactory;

			var busStop = busStopFactory.Create("bus stop of client").WaitAndGetResult(cancellationToken);



			CancelSource = cancelSource;
			InprocessQueueFactory = queueFactory;
			Logger = logger;
			Bus = bus;
			AddressGenerator = addressGenerator;
			SystemTime = systemTime;
			BusStopFactory = busStopFactory;
			BusStop = busStop;
			UniqueTimeStampCreator = uniqueTimeStampCreator;
		}

		public Task<IBusStop> AddNewBusStop(string busStopName)
		{
			return BusStopFactory.Create(busStopName);
		}
	}
}