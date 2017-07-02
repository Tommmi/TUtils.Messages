# Messaging Framework
## Was ist TUtils.Messages ?
`TUtils.Messages` ermöglicht die Errichtung einer virtuellen Kommunikationsinfrastruktur, mittels derer verschiedene 
Programm-Komponenten miteinander kommunizieren. Virtuell ist diese Infrastruktur insofern, dass die miteinander 
kommuniziernden Programm-Komponenten nichts über den physischen Ort oder die verwendete Technologie des genutzten 
Kommunikationsmediums oder des Kommunikationspartners wissen. Für die beteiligten Kommunikationskomponenten hängen 
alle an einem virtuellen Datenbus, unabhängig davon, ob alle Teilkomponenten im selben Thread, im selben Prozess, 
auf der selben Maschine oder auf unterschiedlichen Servern oder unterschiedlichen Clients laufen. 
Jeder kann mit jedem kommunizieren, solange sie am gleichen Datenbus hängen. Bei der Konfiguration der 
konkreten Kommunikationsinfrastruktur können im Prinzip unterschiedlichste Technologien zum Einsatz kommen: 
Inprocess-Queuing, HTTP, IP, Schmalbandfunk, SMS, Email, Windows-Sockets, Shared Memory, WebDAV. 
Alles was im Prinzip geeignet ist, Daten zu transportieren und Signale zu senden, kann genutzt werden.

`TUtils.Messages` verfügt derzeit nur über Implementationen für .NET, HTTP und Inprocess-Queueing. Eine Implementation 
für JavaScript ist derzeit in Arbeit.

`TUtils.Messages` ist Message-orientiert: Die Kommunikation erfolgt asynchron über Messages, was nichts anderes 
sind als Klasseninstanzen, die komplett(!) serialisierbar sind. Bei der Gestaltung der .NET Implementation wurde 
viel Wert darauf gelegt, dass man mittels des Schlüsselworts `await` flüssigen Code schreiben kann, was bei 
asynchronen Schnitstellen nicht unbedingt selbstverständlich ist.

`TUtils.Messages` ist Daten-Bus-orientiert: Alle Kommunikationsmodule registrieren sich an einem Bus und können 
Messages senden und empfangen. 

## Wozu eignet sich TUtils.Messages ?
- TUtils.Messages ermöglicht Message-basierte Kommunikation. Darum ist das **Hauptfeature** die Kommunikation 
zwischen zeitgleich laufenden Vorgängen, Tasks und Threads.
- TUtils.Messages ermöglicht die Virtualisierung der Kommunikationsinfrastruktur. Darum lassen sich leicht 
automatisierte Komponenten- oder Systemtests entwickeln, die unabhängig von der Netzwerkinfrastruktur ablaufen können.
- *Rein theoretische Betrachtung:* Der virtuelle Bus ist vom Prinzip her nicht Client-Server basiert. 
Darum entfällt (applikationslogisch betrachtet) die Rolle des Servers. Zwar können Teilnehmer des Busses Aufgaben eines 
Services übernehmen, aber die Rolle eines zentralen Servers ist nicht mehr zwingend vorgeschrieben. Darum könnte man 
ohne die Programmierung und Wartung eines zentralen Applikations-Servers eine Anwendung betreiben, die trotzdem über 
ein weltweites Kommunikationsnetzwerk verfügt. Wie kann das sein ? Technisch betrachtet benötigt man zur Nutzung des 
Internets als Transportschicht dennoch einen Server, weil die technisch zugrunde liegende Kommunikation 
Client-Server-basiert ist. Dieser Server muss aber nicht zwingend applikationsspezifisch sein, sondern muss lediglich 
einer Anwendung einen globalen Bus zur Verfügung stellen. Eine Anwendung für ein solches Szenario wäre z.B. eine 
firmenintern ad-hoc laufende Teamanwendung, ganz ohne Installation eines Servers durch einen Netzwerkadministrator. 
Da die Technologie keine Rolle spielt, kann die Anwendung auch als JavaScript-Programm in einem Browser arbeiten. 
Im Gegensatz zu einer Webanwendung eines Webservice-Anbieters müssen die Daten nicht bei einem Drittanbieter gehostet werden.
Unabhängig von diesen Überlegungen bleibt aber das Hauptziel von `TUtils.Messages` die Kommunikation zwischen zeitgleich 
laufenden Vorgängen.

## Programmierung
### Voraussetzungen
- .NET 4.6.1
- (andere Plattformen insbesondere JavaScript sind in Arbeit)

### Einbindung
- aktuelle Version [herunterladen](https://github.com/Tommmi/TUtils.Messages/archive/master.zip).
- entpacken
- Folgende Assemblies in das eigene Projekt einbinden
    - `{repository}\lib\TUtils.Common.dll`
    - `{repository}\lib\TUtils.Messages.Common.dll`
    - `{repository}\lib\TUtils.Messages.Core.dll`
    - `{repository}\lib\NetSerializer.dll` (third party [library](https://github.com/tomba/netserializer))


### Lokalen Bus erzeugen
Die einfachste Möglichkeit, einen lokalen Messagebus zu erzeugen, der Messages innerhalb eines Prozesses transportiert, 
ist folgende:

``` CSharp
var logger = new Log4NetWriter();
var simpleMessageEnvironment = new LocalBusEnvironment(logger);
```
In diesem Fall muss man auch das Assembly
    - `{repository}\lib\log4net.dll` einbinden

Natürlich kann man auch andere Logger verwenden:
``` CSharp
// usefull in Unittests:
var logger = new LogConsoleWriter(
    LogSeverityEnum.WARNING,
    namespacesWhiteList: new List<string> { "*" },
    namespacesBlackList: new List<string>());
```

Um einen eigenen Logger zu definieren, muss man das Interface `ILogWriter` implementieren.

`LocalBusEnvironment` ist lediglich eine bequeme Möglichkeit für eine Standard-Initialisierung eines Busses.
Ein Bus ist eine Klasse, die das Interface `IMessageBus` implementiert, wenigstens aber das Interface `IMessageBusBase`.
Hiervon gibt es zwei:
- `MessageBus`: prozess-lokaler Bus
- `BusProxy`: Platzhalter, um auf einen fernen Bus remote zuzugreifen

Um einen lokalen Bus zu erzeugen, muss man also die Klasse `MessageBus` instantiieren, deren Konstruktor eine Reihe von 
DependencyInjection-Parametern verlangt. Instantiieren Sie `MessageBus` manuell, um das Standardverhalten detailliert 
zu beeinflussen.

> Die MessageBus-Implementation `MessageBus` führt aus Performancegründen keine Serialisierung oder Deserialisierung 
der Messages aus. Die übergebenen Message-Objekte werden also als Referenz direkt weitergegeben. Ein Empfänger 
einer Message sollte deshalb nie die erhaltene Message modifizieren.

### Messages senden
Zwar liefert ein Bus (`IMessageBus`) bereits eine Möglichkeit, Messages zu senden 
``` CSharp
await simpleMessageEnvironment.Bus.SendPort.Enqueue(new MyMessage());
```
dennoch wird empfohlen, nur über einen Busstop auf den Bus zuzugreifen. Ein Busstop ist eine Klasse, die das 
Interface `IBusStop` implementiert. Ein BusStop verfügt über eine eigene globale Adresse, die in Messages 
mitangegeben werden kann. Messages, die das Interface `IAddressedMessage` implementieren, verfügen über eine 
Zieladresse. Messages, die das Interface `IRequestMessage` implementieren, verfügen außerdem über eine Quelladresse, 
die vom BusStop beim Senden automatisch gesetzt wird. 
> In einem Bus bestimmen nicht die Sender einer Message, wer die Message erhält. Stattdessen registrieren sich 
die Empfänger für bestimmte Messages. So kann man sich in einem BusStop für Messages registrieren, die die gleiche 
Zieladresse haben, wie der BusStop. Es ist aber auch möglich, jede beliebige andere Message zu erhalten, wenn man will.

`LocalBusEnvironment` definiert bereits einen Default-Busstop:
``` CSharp
IBusStop LocalBusEnvironment.BusStop { get; }
```
Sie können aber aber auch einen neuen BusStop erzeugen:
``` CSharp
Task<IBusStop> LocalBusEnvironment.AddNewBusStop(string busStopName)
```

Für das Senden einer Message sind folgende Methoden in einem BusStop vorgesehen:
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

### **Messages empfangen**
Grundsätzlich gibt es zwei Möglichkeiten, Messages zu empfangen:
#### 1. Messagehandler registrieren
Man registriert einen Delegate, der bei Empfang einer bestimmten Message automatisch aufgerufen wird  

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
Nur auf Messages reagieren, für die ein bestimmtes Filterkriterium passt.
``` CSharp
_env.BusStop
	.On<MyRequestMessage>()
	.FilteredBy(msg => _allowedSources.Contains(msg.Source))
	.Do(OnMyRequestMessage);
```

**.IncludingMessagesToOtherBusStops( )**  
Auch auf Messages reagieren, die nicht an die Addresse des BusStops gesendet wurden.
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
Man wartet im aktuellen Codefluss asynchron auf den Empfang der Message

**Send( )**  
Eine Message vom Typ IRequestMessage senden und auf den Empfang der zugehörigen ResponseMessage warten, die an 
die Adresse dieses BusStops gesendet wurde.
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
	diconnectedRetryIntervallTimeMs: 30 * 1000);
await envClient.ConnectToServer(new Uri("http://localhost:8097"));
var defaultBustStop = envClient.BusStop;
var result = await defaultBustStop.SendWithTimeoutAndRetry<TestRequestMessage, TestResponseMessage>(
	new TestRequestMessage(envServer.BusStop.BusStopAddress, "hello world"));
```

> Alle Messages, die über das Netzwerk transportiert werden, müssen serialisierbar sein. Darum müssen alle  Message-Klassendefinitionen das Attribut **[Serializable]** aufweisen ! Sie dürfen **nicht** das Interface ISerializable
aufweisen ! Wenn sich nicht alle verwendeten Messages in Assembly.GetEntry() oder einem Sub-Assembly befinden (z.B. in
Unittests ist das der Fall), muss man die Assemblies bei der Initialisierung manuell hinzufügen (siehe Konstruktor
ClientStandardEnvironment(..)). 

#### Server
``` CSharp
var logger = new Log4NetWriter();
var envServer = new ServerStandardEnvironment(logger,timeoutForLongPollingRequest:2*60*1000);
var defaultBustStop = envServer.BusStop;

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










