using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TUtils.Common;
using TUtils.Common.Logging;
using TUtils.Common.Logging.Common;
using TUtils.Messages.Common;
using TUtils.Messages.Common.Bus;
using TUtils.Messages.Common.BusStop;
using TUtils.Messages.Common.Common;
using TUtils.Messages.Common.Queue;
using TUtils.Messages.Core.Bus;
using TUtils.Messages.Core.Net;
using TUtils.Messages.Core.Serializer;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace TUtils.Messages.Core
{
	public class ClientStandardEnvironment
	{
		#region fields

		private readonly int _requestRetryIntervallTimeMs;
		private readonly INetClientFactory _netClientFactory;
		private readonly IMessageBusBaseProtocol _messageBusBaseProtocol;
		private readonly IndexedTable<Uri, BusProxy, NetClientQueue> _netQueues = new IndexedTable<Uri, BusProxy, NetClientQueue>();
		private readonly object _sync = new object();

		#endregion

		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		#region public

		public string ClientUri { get; }
		public IBusStop BusStop { get; }
		public IMessageBus Bus { get; }
		public ITLog Logger { get; }
		public IQueueFactory QueueFactory { get; }
		public CancellationToken CancellationToken { get; }
		public CancellationTokenSource CancelSource { get; }
		public IMessageSerializer Serializer { get; }
		public ISystemTimeProvider SystemTime { get; }
		public Bridge Bridge { get; }

		public async Task<IMessageBusBase> ConnectToServer(Uri serverAddress)
		{
			var netClientQueue = new NetClientQueue(_netClientFactory, Serializer, Logger, SystemTime, serverAddress, _requestRetryIntervallTimeMs);
			var busProxy = new BusProxy(netClientQueue, netClientQueue, _messageBusBaseProtocol, UniqueTimeStampCreator, CancellationToken, Logger);
			await Bridge.AddBus(busProxy);
			lock (_sync)
			{
				_netQueues.Insert(new Tuple<Uri, BusProxy, NetClientQueue>(serverAddress, busProxy, netClientQueue));
			}
			return busProxy;
		}

		// ReSharper disable once UnusedMember.Global
		public void DisconnectFromServer(Uri serverAddress)
		{
			Tuple<Uri, BusProxy, NetClientQueue> netQueue;
			lock (_sync)
			{
				netQueue = _netQueues.FindByItem1(serverAddress).FirstOrDefault();
				_netQueues.RemoveAllMatchingItem1(serverAddress);
			}

			if (netQueue != null)
			{
				var busProxy = netQueue.Item2;
				var queue = netQueue.Item3;
				Bridge.RemoveBus(busProxy);
				queue.Dispose();
				busProxy.Dispose();
			}

		}

		#endregion

		#region constructor


		/// <summary>
		/// 
		/// </summary>
		/// <param name="logWriter"></param>
		/// <param name="clientUri"></param>
		/// <param name="additionalConfiguration">may be null</param>
		/// <param name="requestRetryIntervallTimeMs">
		/// how many milli seconds should the client wait at minimum between two failed polling requests ?
		/// Note ! if a server isn't available the client will retry to connect it with this 
		/// intervall time. This value hasn't any effect on the time between two successfully requests.
		/// The long polling timeout shouldn't be smaller than requestRetryIntervallTimeMs !
		/// </param>
		/// <param name="rootAssemblies">
		/// assemblies, which contains serializable types.
		/// Referenced assemblies will be included automatically.
		/// If nothing is specified the default is Assembly.GetEntry()
		/// For Unittests use Assembly.GetAssembly(GetType())
		/// </param>
		public ClientStandardEnvironment(
			ILogWriter logWriter,
			string clientUri,
			Action<HttpClient> additionalConfiguration,
			int requestRetryIntervallTimeMs,
			params Assembly[] rootAssemblies)
		{
			var localBusEnvironment = new LocalBusEnvironment(logWriter);
			_requestRetryIntervallTimeMs = requestRetryIntervallTimeMs;
			ClientUri = clientUri;
			CancelSource = localBusEnvironment.CancelSource;
			CancellationToken = CancelSource.Token;
			QueueFactory = localBusEnvironment.InprocessQueueFactory;
			UniqueTimeStampCreator = localBusEnvironment.UniqueTimeStampCreator;
			Logger = localBusEnvironment.Logger;
			SystemTime = localBusEnvironment.SystemTime;
			_messageBusBaseProtocol = new MessageBusBaseProtocol();
			Serializer = new MessageSerializer(
				rootAssemblies: rootAssemblies.ToList(),
				blacklistFilter: type => false,
				additionalTypes: null);

			Bus = localBusEnvironment.Bus;
			BusStop = localBusEnvironment.BusStop;
			Bridge = new Bridge(Logger);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Bridge.AddBus(Bus).LogExceptions(Logger);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_netClientFactory = new NetClientFactory(clientUri, CancellationToken, Logger, additionalConfiguration);
		}

		public IUniqueTimeStampCreator UniqueTimeStampCreator { get; set; }

		#endregion
	}
}