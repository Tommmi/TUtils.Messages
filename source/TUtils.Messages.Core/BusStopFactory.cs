using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;

namespace TUtils.Messages.Core
{
	public class BusStopFactory : IBusStopFactory
	{
		private readonly IMessageBus _bus;
		private readonly IUniqueTimeStampCreator _uniqueTimeStampCreator;
		private readonly IAddressGenerator _addressGenerator;
		private readonly CancellationToken _cancellationToken;
		private readonly ISystemTimeProvider _time;
		private readonly long _defaultTimeoutMs;

		public BusStopFactory(
			IMessageBus bus,
			IUniqueTimeStampCreator uniqueTimeStampCreator,
			IQueueFactory queueFactory,
			IAddressGenerator addressGenerator,
			CancellationToken cancellationToken,
			ISystemTimeProvider time,
			long defaultTimeoutMs)
		{
			_bus = bus;
			_uniqueTimeStampCreator = uniqueTimeStampCreator;
			_addressGenerator = addressGenerator;
			_cancellationToken = cancellationToken;
			_time = time;
			_defaultTimeoutMs = defaultTimeoutMs;
		}

		Task<IBusStop> IBusStopFactory.Create(string nodeName)
		{
			IAddress address = _addressGenerator.Create(nodeName);
			return (this as IBusStopFactory).Create(address);
		}

		Task<IBusStop> IBusStopFactory.Create(IAddress address)
		{
			return new BusStop.BusStop().Init(
				_bus,
				address,
				_uniqueTimeStampCreator,
				_cancellationToken,
				_time,
				_defaultTimeoutMs);
		}
	}
}