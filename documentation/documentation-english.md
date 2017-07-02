# Messaging Framework
## What is TUtils.Messages ?
`TUtils.Messages` allows the establishment of a virtual communication infrastructure, by means of which different
program components are able to communicate with each other. "Virtual" means that the communicating program components 
don't know anything about the location or the technology used for the communication medium or the communication component.
For all of the participating communication components it looks as if they all are attached to a virtual data bus,
regardless of whether the sub-components are running in the same thread, in the same process,
run on the same machine or on different servers or different clients.
Everyone can communicate with each other as long as they are attached to the same bus. While configuring the
specific communication infrastructure there may appear different technologies:
inprocess queuing, HTTP, IP, narrow band radio, SMS, Email, Windows sockets, shared memory, WebDAV.
All that is basically suitable to carry data and to transmit signals, could be used 
(as long as there exists an implemention for that technology or medium).

`TUtils.Messages` currently only has implementations for .NET, HTTP and inprocess queueing. An implementation for 
JavaScript is currently in progress.

`TUtils.Messages` is message-oriented: The communication takes place asynchronously using messages, which are nothing more than 
class instances that are completely (!) serializable. When designing the .NET implementation of this messaging framework
it has placed great emphasis on the potentiality to write fluent code by using the keyword `await`.

`TUtils.Messages` is data-bus-oriented: All communication modules will be registered at a bus and can send and receive messages.

## What is `TUtils.Messages` for ?
- `TUtils.Messages` enables message-based communication. Therefore, the **main feature** is the communication
between simultaneously running processes, tasks and threads.
- `TUtils.Messages` enables virtualization of the communication infrastructure. Therefore, automated component or system tests 
can be easily developed in a way that they can run independently of the network infrastructure.
- *(Pure academic considerations:)* Basically the virtual bus is not client-server-oriented.
Therefore (considered from application logic perspective) the role of the server is omitted. 
Although participants can take the role of a service, the role of a central server isn't mandatory anymore. 
Therefore it's possible to operate an application that uses a global communication network without the need of 
programming and maintaining a central application server. How can that be? From a technical perspective you do need 
a server to use the Internet as a transport layer, because underlying communication technology is client-server-based. 
But this server needn't necessarily be specific to the application, but must simply provide a global bus to the application.
An applying for such a scenario could be (for example) a company-internal ad-hoc ongoing team application, 
without the need of installing a server by a network administrator.
Since the technology does not matter, the application can also work as a JavaScript program in a browser.
Unlike a web application of a web service provider, the data needn't be hosted by a third party.
Independently of these considerations, however, the main objective of `TUtils.Messages` remains communication 
between simultaneously running operations.

## Programming
### Requirements
- .NET 4.6.1
- (other platforms particular JavaScript are in process)

### Installation
- [Download](https://github.com/Tommmi/TUtils.Messages/archive/master.zip) current version .
- unpack
- Bind following assemblies into your own project:
    - `{repository}\lib\TUtils.Common.dll`
    - `{repository}\lib\TUtils.Messages.Common.dll`
    - `{repository}\lib\TUtils.Messages.Core.dll`
    - `{repository}\lib\NetSerializer.dll` (third party [library](https://github.com/tomba/netserializer))


### Create A Local Bus
The simplest way to generate a local message bus, which transports messages within a process, is the following:

``` CSharp
var logger = new Log4NetWriter();
var simpleMessageEnvironment = new LocalBusEnvironment(logger);
```
In this case you must include the following assemby also:
    - `{repository}\lib\log4net.dll` einbinden

Of course you can also use another logger:
``` CSharp
// usefull in Unittests:
var logger = new LogConsoleWriter(
    LogSeverityEnum.WARNING,
    namespacesWhiteList: new List<string> { "*" },
    namespacesBlackList: new List<string>());
```

To define your own logger, you have to implement the interface `ILogWriter`.

`LocalBusEnvironment` is merely a convenient way for a default initialization of a bus.
A bus is a class that implements the interface `IMessageBus`, but at least the interface` IMessageBusBase`.
Of these, there are two:
- `MessageBus`: in-process (local) Bus
- `BusProxy`: place holder, to access a remote bus.

So in order to generate a local bus, you have to instantiate the class `MessageBus` whose constructor requires a series of
dependency injection parameters. Instantiate `MessageBus` manually to influence the default behavior in detail.

> Due to performance issues the implementation of `MessageBus` doesn't execute any serialization or deserialization of the messages.
So the passed messages will be transfered directly as references. Therefore a receiver of a message should never modify the received message.

### Send Messages
Though a bus (`IMessageBus`) provides a way to send messages already 
``` CSharp
await simpleMessageEnvironment.Bus.SendPort.Enqueue(new MyMessage());
```
it is strongly recommended to access the bus only over a bus stop. A bus stop is a class which implements 
interface `IBusStop`. A BusStop has its own global address, which can be passed in messages. 
Messages that implement the interface `IAddressedMessage`, have a
destination address. Messages that implement the interface `IRequestMessage`, also have a source address,
which is automatically set by BusStop when sending a message.

> In a bus it's not up to the sender of a message, to determine the receiver of the message. Instead of that
the receiver determines which messages it receives. So you can register in a BusStop for messages, 
which have the same destination address as the BusStop. But it is also possible to obtain any other message, if you want.

`LocalBusEnvironment` already defines a default BusStop:
``` CSharp
IBusStop LocalBusEnvironment.BusStop { get; }
```
But you may add new bus stops if you want:
``` CSharp
Task<IBusStop> LocalBusEnvironment.AddNewBusStop(string busStopName)
```


The following methods are provided in a BusStop for sending a message:
``` CSharp
/// <summary>
/// Posts message "message" into bus.
/// If "message" implements interface IAddressedMessage, this method will set property message.Source automatically.
/// </summary>
/// <param name="message"></param>
void Post(object message);

/// <summary>
/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
/// a message of type TResponse has been received with same request id "request.RequestId".
/// request.RequestId and request.Source will be set by this method Send() automatically.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="request"></param>
/// <returns></returns>
Task<TResponse> Send<TRequest, TResponse>(TRequest request)
	where TRequest : IRequestMessage
	where TResponse : IResponseMessage;

/// <summary>
/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
/// elapsed (see constructor parameter).
/// request.RequestId and request.Source will be set by this method Send() automatically.
/// SendWithTimeoutAndRetry() will retry to send the request up to 10 times within the given timeout.
/// The polling intervall time will be increased be each retry, but won't be smaller than 100 ms.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="request"></param>
/// <returns></returns>
Task<TimeoutResult<TResponse>> SendWithTimeoutAndRetry<TRequest, TResponse>(TRequest request)
	where TRequest : IRequestMessage
	where TResponse : IResponseMessage;

/// <summary>
/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
/// elapsed (see constructor parameter).
/// request.RequestId and request.Source will be set by this method Send() automatically.
/// This method starts only one request.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="request"></param>
/// <returns></returns>
Task<TimeoutResult<TResponse>> SendWithTimeout<TRequest, TResponse>(TRequest request)
	where TRequest : IRequestMessage
	where TResponse : IResponseMessage;

/// <summary>
/// Sends message "request" to request.Destination and returns a waiting task, which will complete only when 
/// a message of type TResponse has been received with same request id "request.RequestId" or given timeout 
/// elapsed.
/// request.RequestId and request.Source will be set by this method Send() automatically.
/// This method starts only one request.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="request"></param>
/// <param name="timeoutMs"></param>
/// <returns></returns>
Task<TimeoutResult<TResponse>> SendWithTimeout<TRequest, TResponse>(TRequest request, long timeoutMs)
	where TRequest : IRequestMessage
	where TResponse : IResponseMessage;
```

### **Receive Messages**
There are basically two ways to receive messages:
#### 1. Register a message handler
Register a delegate which is called automatically when receiving a specific message

**IBusStop.On<{MessageType}>( )**  
``` CSharp
async void Test()
{
    var logger = new Log4NetWriter();
    _env = new LocalBusEnvironment(logger);
    _env.BusStop
    	.On<MyRequestMessage>()
    	.Do(OnMyRequestMessage);
}

async Task OnMyRequestMessage(MyRequestMessage myRequestMessage, CancellationToken cancellationToken)
{
	// dosth ...
	// cancellationToken.ThrowIfCancellationRequested();

	// post response
	_env.BusStop.Post(new MyResponseMessage(priority:1,requestMessage:myRequestMessage));
}
```

**.FilteredBy( )**  
To handle only messages for which fits a given filter criteria.
``` CSharp
_env.BusStop
	.On<MyRequestMessage>()
	.FilteredBy(msg => _allowedSources.Contains(msg.Source))
	.Do(OnMyRequestMessage);
```

**.IncludingMessagesToOtherBusStops( )**  
Also respond to messages that were not sent to the address of this bus stop.
``` CSharp
_env.BusStop
	.On<MyRequestMessage>()
	.IncludingMessagesToOtherBusStops()
	.Do(OnMyRequestMessage);
```
``` CSharp
_env.BusStop
	.On<MyRequestMessage>()
	.IncludingMessagesToOtherBusStops()
	.FilteredBy(msg => _allowedSources.Contains(msg.Source))
	.Do(OnMyRequestMessage);
```



#### 2. Await
The second way to receive messages is to wait asynchronously in the current code flow for a message.

**Send( )**  
Send a message of type IRequestMessage and wait for the receipt of the corresponding IResponseMessage.
``` CSharp
async void Test()
{
    var request = new MyRequestMessage(priority: 1, destination: _myService);
    var response = await _env.BusStop.Send<MyRequestMessage,MyResponseMessage>(request);
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
	// will be set automatically
	public IAddress Source { get; set; }
	// will be set automatically by Send() method
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
	// will be set automatically
	public IAddress Source { get; set; }
	public long RequestId { get; set; }
}
```
> **IPrioMessage**  
Messages implementing interface `IPrioMessage` will be prefered or disadvantaged according to their priority. 
A message without implementing interface `IPrioMessage` implicitly uses the priority **200**.

**SendWithTimeout( )**  
Uses the default timeout that was set in `LocalBusEnvironment` (20 seconds).
``` CSharp
var timeOutResult = await _env.BusStop.SendWithTimeout<MyRequestMessage, MyResponseMessage>(request);
if (!timeOutResult.TimeoutElapsed)
{
	response = timeOutResult.Value;
}
```
**WaitOnMessageToMe( )**  
Waiting for a message that has been sent to the address of the BusStop.
``` CSharp
var request = new MyRequestMessage(priority: 1, destination: _myService);
request.RequestId = 5672374;
_env.BusStop.Post(request);
response = await _env.BusStop.WaitOnMessageToMe<MyResponseMessage>(msg => msg.RequestId == request.RequestId);
```
or
``` CSharp
var timeOutResult = await _env.BusStop.WaitOnMessageToMe<MyResponseMessage>(
	timeoutMs:2000,
	filter:msg => msg.RequestId == request.RequestId && _allowedSources.Contains(msg.Source));
if (!timeOutResult.TimeoutElapsed)
{
	response = timeOutResult.Value;
}
```

### WaitForIdle()
One of the most difficult problems to be analyzed in multi-task systems is, that individual services can be overloaded
and the task load continues to grow. This manifests itself in the fact that queues always continue to grow. 
Because non-overloaded sub-systems don't know that there are some other overloaded subsystems, 
they create more and more requests to the system and overload thereby the system all the more. 
This is a problem category, which is practically unknown in single-threaded programs with synchronous internal communication. 
A very good solution for this is to design most messages as request / response pairs
and to wait always for the completion of the remote message processing. Such a procedure, however, 
slows down working performance and partially shoots down the benefits of massively parallel processing. 
Another possibility is, to lower prioritize messages of long-during tasks and to delayed their processing (`IPrioMessage`).
A third interesting solution, which is offered in this framework, is the following method call:
``` CSharp
Task IBusStop.WaitForIdle()
```
WaitForIdle returns a task, which only completes when the bus has no more than 5 messages processing currently. 
The limit for the message number can be defined during the initialization of the MessageBus.
`LocalBusEnvironment` defines 5 as default.

### Creating a global bus
So far, only scenarios were discussed, where messages were sent within a single process.
To leave the process boundaries, local buses of the various processes or machines have to be connected with each 
other to one single global bus. The easiest way to do this is to use the ready-to-use configurations
`ClientStandardEnvironment` and `ServerStandardEnvironment`. Use `ClientStandardEnvironment` for
operating system processes which operate in networks as a client. Use `ServerStandardEnvironment` for
operating system processes which operate in networks as a central server. `LocalBusEnvironment` is no longer
used.

#### Client-PC
``` CSharp
var logger = new Log4NetWriter();
var envClient = new ClientStandardEnvironment(
	logWriter:logger,
	clientUri: "fooBarClient", // any unique string
	additionalConfiguration: null,
	diconnectedRetryIntervallTimeMs: 30 * 1000);
await envClient.ConnectToServer(new Uri("http://localhost:8097"));
var defaultBustStop = envClient.BusStop;
var result = await defaultBustStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
	new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));
```

> All messages that are being transported over the network must be serializable. 
Therefore all message class definitions must have the attribute **[Serializable]**! 
Do **not** use the interface `ISerializable`! If there are message classes, which aren't defined in the entry assembly (`Assembly.GetEntry()`) 
or in one of its sub-assemblies (as in unit testing is the case), you have to manually add the assemblies 
during initialization (see constructor of `ClientStandardEnvironment').

#### Server
``` CSharp
var logger = new Log4NetWriter();
var envServer = new ServerStandardEnvironment(logger, timeoutForLongPollingRequest:2*60*1000);
var defaultBustStop = envClient.BusStop;

try
{
	var httpServerTask = new SimpleHttpServer(
		envServer.NetServer,
		envServer.CancellationToken,
		envServer.Logger,
		listenPort:8097); // or sth else
	httpServerTask.Start();
	await httpServerTask.WaitForTermination();
}
catch (Exception e)
{
	envServer.Logger.LogException(e);
}
```










