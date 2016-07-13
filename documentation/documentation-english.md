# Messaging Framework
## What is TUtils.Messages ?
`TUtils.Messages` allows the establishment of a virtual communication infrastructure, by means of which different
program components are able to communicate with each other. "Virtual" means that the communicating program components 
don't know anything about the location or the technology used for the communication medium or the communication component.
For all of the participating communication components it looks as if they all are attached to a virtual data bus,
regardless of whether the sub-components are running in the same thread, in the same process,
run on the same machine or on different servers or different clients.
Everyone can communicate with each other as long as they are attached to the same bus. When configuring the
specific communication infrastructure basically there may be used different technologies:
Inprocess Queuing, HTTP, IP, narrow band radio, SMS, Email, Windows sockets, shared memory, WebDAV.
All that is basically suitable to carry data and to transmit signals, could be used (as long as there exists an implemention for that technology or medium).

`TUtils.Messages` currently only has implementations for .NET, HTTP and inprocess queueing. An implementation for 
JavaScript is currently in progress.

`TUtils.Messages` is message-oriented: The communication takes place asynchronously using messages, which are nothing more than 
class instances that are completely (!) serializable. When designing the .NET implementation of this messaging framework
it has placed great emphasis on the potentiality to write fluent code by using the keyword `await`.

`TUtils.Messages` is data-bus-oriented: All communication modules will be registered at a bus and can send and receive messages.

## What is `TUtils.Messages` for ?
- `TUtils.Messages` enables message-based communication. Therefore, the main feature is the communication
between simultaneously running processes, tasks and threads.
- `TUtils.Messages` enables virtualization of the communication infrastructure. Therefore, automated component or system tests 
can be easily developed in a way that they can run independently of the network infrastructure.
- *Pure academic considerations:* Basically the virtual bus is not client-server-oriented.
Therefore (considered from application logic perspective) the role of the server is omitted. 
Although participants can take the role of a service, the role of a central server isn't mandatory anymore. 
Therefore it's possible to operate an application that uses a global communication network without the need of 
programming and maintaining a central application server. How can that be ? From a technical perspective you do need 
a server to use the Internet as a transport layer, because underlying communication technology is client-server-based. 
But this server needn't necessarily be specific to the application, but must simply provide a global bus to the application.
An applying for such a scenario could be (for example) an company-internal ad-hoc ongoing team application, 
without the need of installing a server by a network administrator.
Since the technology does not matter, the application can also work as a JavaScript program in a browser.
Unlike a web application of a web service provider, the data needn't be hosted by a third party.
Independently of these considerations, however, the main objective of `TUtils.Messages` remains communication 
between simultaneously running operations.

## Programming
### Requirements
- .NET 4.6.1
- (other platforms particular JavaScript are in process)

### Einbindung
- [Download](https://github.com/Tommmi/TUtils.Messages/archive/master.zip) current version .
- unpack
- Bind following assemblies into your own project:
    - `{repository}\lib\TUtils.Common.dll`
    - `{repository}\lib\TUtils.Messages.Common.dll`
    - `{repository}\lib\TUtils.Messages.Core.dll`
    - `{repository}\lib\NetSerializer.dll` (third party [library](https://github.com/tomba/netserializer))


### Create Local Bus
The simplest way to generate a local messagebus, which transports messages within a process, is the following:

``` CSharp
var logger = new Log4NetWriter();
var simpleMessageEnvironment = new LocalBusEnvironment(logger);
```

Of course you can also use other logger:
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

`Local Bus Environment` already defines a default BusStop:
``` CSharp
IBusStop LocalBusEnvironment.BusStop { get; }
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
Messages, die `IPrioMessage` implementieren, werden gemäß der gesetzten Priorität gegenüber anderen Messages bevorzugt 
oder benachteiligt. Eine Message ohne implementiertes Interface `IPrioMessage` verwendet implizit die Priorität **200**.

**SendWithTimeout( )**  
Verwendet den Default-Timeout, der in LocalBusEnvironment gesetzt wurde (20 Sekunden).
``` CSharp
var timeOutResult = await _env.BusStop.SendWithTimeout<MyRequestMessage, MyResponseMessage>(request);
if (!timeOutResult.TimeoutElapsed)
{
	response = timeOutResult.Value;
}
```
**WaitOnMessageToMe( )**  
Allgemein auf eine Message warten, die an die Adresse des BusStops gesendet wurde.
``` CSharp
var request = new MyRequestMessage(priority: 1, destination: _myService);
request.RequestId = 5672374;
_env.BusStop.Post(request);
response = await _env.BusStop.WaitOnMessageToMe<MyResponseMessage>(msg => msg.RequestId == request.RequestId);
```
oder
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
Eine der am schwierigsten zu analysierenden Probleme mehrläufiger Systeme ist, dass einzelne Services überlastet 
werden können und die Aufgabenlast immer weiter wächst, aber nicht schnell genug abgearbeitet werden kann. 
Das äußerst sich darin, dass Queues immer weiter anwachsen. Weil nicht überlastete Komponenten/Abläufe nicht wissen, 
dass es einzelne andere überlastete Teilsysteme gibt, stellen sie immer weitere Aufgaben an das System und überlasten 
es dadurch nur um so mehr. Das ist eine Problemkategorie, die singlethreaded Programme mit synchroner innerer 
Kommunikation praktisch nicht kennen. Eine sehr gute Lösung hierfür ist, die meisten Messages als Request/Response-Paare 
zu gestalten und immer auf die Beendigung der fernen Message-Verarbeitung zu warten. Eine solche Vorgehensweise macht 
allerdings zum Teil die Vorteile einer massiv parallelen Verarbeitung zunichte. Eine andere Möglichkeit ist, Messages, 
deren Verarbeitung verzögert werden darf und langwierige Verarbeitungen anstoßen, niedrig zu priorisieren (`IPrioMessage`).
Eine dritte interessante Lösung, die in diesem Framwork angeboten wird, ist der Aufruf der Methode 
``` CSharp
Task IBusStop.WaitForIdle()
```
WaitForIdle liefert einen Task zurück, der erst completed, wenn im Bus nicht mehr als 5 Messages gerade von einem 
Handler verarbeitet werden. Die Grenze für die Messageanzahl ist bei der Initialisierung des Busses definierbar, 
LocalBusEnvironment definiert 5 als Default.

### Einen globalen Bus erzeugen
Bisher wurden nur Szenarien erörtert, bei dem Messages innerhalb ein und deselben Prozesses gesendet wurden.
Um die Prozessgrenzen zu verlassen, muss man lokale Busse verschiedener Prozesse oder Maschinen miteinander zu einem
gemeinsammen Bus verbinden.
Die einfachste Möglichkeit hierzu ist, die fertigen Konfigurationen 
`ClientStandardEnvironment` und `ServerStandardEnvironment` zu verwenden. `ClientStandardEnvironment` verwendet man für
Betriebssystemprozesse, die netzwerktechnisch als Client arbeiten, `ServerStandardEnvironment` verwendet man für den
Betriebssystemprozess, der netzwerktechnisch als zentraler Server arbeitet. `LocalBusEnvironment` wird gar nicht mehr
verwendet.
#### Client-PC
``` CSharp
var logger = new Log4NetWriter();
var envClient = new ClientStandardEnvironment(
	logWriter:logger,
	clientUri: "fooBarClient", // any unique string
	additionalConfiguration: null,
	requestRetryIntervallTimeMs: 30 * 1000);
var defaultBustStop = envClient.BusStop;
```

> Alle Messages, die über das Netzwerk transportiert werden, müssen serialisierbar sein. Darum müssen alle  Message-Klassendefinitionen das Attribut **[Serializable]** aufweisen ! Sie dürfen **nicht** das Interface ISerializable
aufweisen ! Wenn sich nicht alle verwendeten Messages in Assembly.GetEntry() oder einem Sub-Assembly befinden (z.B. in
Unittests ist das der Fall), muss man die Assemblies bei der Initialisierung manuell hinzufügen (siehe Konstruktor
ClientStandardEnvironment(..)). 

#### Server
``` CSharp
var logger = new Log4NetWriter();
var envServer = new ServerStandardEnvironment(logger);
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










