# Messaging Framework
## Was ist TUtils.Messages ?
`TUtils.Messages` erm�glicht die Errichtung einer virtuellen Kommunikationsinfrastruktur, mittels derer verschiedene 
Programm-Komponenten miteinander kommunizieren. Virtuell ist diese Infrastruktur insofern, dass die miteinander 
kommuniziernden Programm-Komponenten nichts �ber den physischen Ort oder die verwendete Technologie des genutzten 
Kommunikationsmediums oder des Kommunikationspartners wissen. F�r die beteiligten Kommunikationskomponenten h�ngen 
alle an einem virtuellen Datenbus, unabh�ngig davon, ob alle Teilkomponenten im selben Thread, im selben Prozess, 
auf der selben Maschine oder auf unterschiedlichen Servern oder unterschiedlichen Clients laufen. 
Jeder kann mit jedem kommunizieren, solange sie am gleichen Datenbus h�ngen. Bei der Konfiguration der 
konkreten Kommunikationsinfrastruktur k�nnen im Prinzip unterschiedlichste Technologien zum Einsatz kommen: 
Inprocess-Queuing, HTTP, IP, Schmalbandfunk, SMS, Email, Windows-Sockets, Shared Memory, WebDAV. 
Alles was im Prinzip geeignet ist, Daten zu transportieren und Signale zu senden, kann genutzt werden.

`TUtils.Messages` verf�gt derzeit nur �ber Implementationen f�r .NET. Eine Implementation f�r JavaScript ist derzeit in Arbeit.

`TUtils.Messages` ist Message-orientiert: Die Kommunikation erfolgt asynchron �ber Messages, was nichts anderes 
sind als Klasseninstanzen, die komplett(!) serialisierbar sind. Bei der Gestaltung der .NET Implementation wurde 
viel Wert darauf gelegt, dass man mittels des Schl�sselworts `await` fl�ssigen Code schreiben kann, was bei 
asynchronen Schnitstellen nicht unbedingt selbstverst�ndlich ist.

`TUtils.Messages` ist Daten-Bus-orientiert: Alle Kommunikationsmodule registrieren sich an einem Bus und k�nnen 
Messages senden und empfangen. 

## Wozu eignet sich TUtils.Messages ?
- TUtils.Messages erm�glicht Message-basierte Kommunikation. Darum ist das **Hauptfeature** die Kommunikation 
zwischen zeitgleich laufenden Vorg�ngen, Tasks und Threads.
- TUtils.Messages erm�glicht die Virtualisierung der Kommunikationsinfrastruktur. Darum lassen sich leicht 
automatisierte Komponenten- oder Systemtests entwickeln, die unabh�ngig von der Netzwerkinfrastruktur ablaufen k�nnen.
- *Rein theoretische Betrachtung:* Der virtuelle Bus ist vom Prinzip her nicht Client-Server basiert. 
Darum entf�llt (fachlich betrachtet) die Rolle des Servers. Zwar k�nnen Teilnehmer des Busses Aufgaben eines Services �bernehmen, aber die Rolle eines zentralen Servers ist nicht mehr zwingend vorgeschrieben. Darum k�nnte man ohne die Programmierung und Wartung eines zentralen Applikations-Servers eine Anwendung betreiben, die trotzdem �ber ein weltweites 
Kommunikationsnetzwerk verf�gt. Wie kann das sein ? Technisch betrachtet ben�tigt man zur Nutzung des Internets als Transportschicht dennoch einen Server, weil die technisch zugrunde liegende Kommunikation Client-Server-basiert ist. Dieser Server muss aber nicht zwingend applikationsspezifisch sein, sondern muss lediglich einer Anwendung einen globalen Bus 
zur Verf�gung stellen. Eine Anwendung f�r ein solches Szenario w�re z.B. eine firmenintern ad-hoc laufende Teamanwendung, 
ganz ohne Installation eines Servers durch einen Netzwerkadministrator. Da die Technologie keine Rolle spielt, 
kann die Anwendung auch als JavaScript-Programm in einem Browser arbeiten. Im Gegensatz zu einer Webanwendung eines Webservice-Anbieters m�ssen die Daten nicht bei einem Drittanbieter gehostet werden.  
Unabh�ngig von diesen �berlegungen bleibt aber das Hauptziel von `TUtils.Messages` die Kommunikation zwischen zeitgleich laufenden Vorg�ngen.

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
Die einfachste M�glichkeit, einen lokalen Messagebus zu erzeugen, der Messages innerhalb eines Prozesses transportiert, ist folgende:

``` CSharp
var logger = new Log4NetWriter();
var simpleMessageEnvironment = new LocalBusEnvironment(logger);
```

Nat�rlich kann man auch andere Logger verwenden:
``` CSharp
// usefull in Unittests:
var logger = new LogConsoleWriter(
    LogSeverityEnum.WARNING,
    namespacesWhiteList: new List<string> { "*" },
    namespacesBlackList: new List<string>());
```

Um einen eigenen Logger zu definieren, muss man das Interface `ILogWriter` implementieren.

`LocalBusEnvironment` ist lediglich eine bequeme M�glichkeit f�r eine Standard-Initialisierung eines Busses.
Ein Bus ist eine Klasse, die das Interface `IMessageBus` implementiert, wenigstens aber das Interface `IMessageBusBase`.
Hiervon gibt es zwei:
- `MessageBus`: prozess-lokaler Bus
- `BusProxy`: Platzhalter, um auf einen fernen Bus remote zuzugreifen

Um einen lokalen Bus zu erzeugen, muss man also die Klasse `MessageBus` instantiieren, deren Konstruktor eine Reihe von DependencyInjection-Parametern verlangt. Instantiieren Sie `MessageBus` manuell, um das Standardverhalten detailliert zu beeinflussen.

> Die MessageBus-Implementation `MessageBus` f�hrt aus Performancegr�nden keine Serialisierung oder Deserialisierung der Messages aus. Die �bergebenen Message-Objekte werden also als Referenz direkt weitergegeben. Ein Empf�nger einer Message sollte deshalb nie die erhaltene Message modifizieren.

### Messages senden
Zwar liefert ein Bus (`IMessageBus`) bereits eine M�glichkeit, Messages zu senden 
``` CSharp
await simpleMessageEnvironment.Bus.SendPort.Enqueue(new MyMessage());
```
dennoch wird empfohlen, nur �ber einen Busstop auf den Bus zuzugreifen. Ein Busstop ist eine Klasse, die das Interface `IBusStop` implementiert. 
Ein BusStop verf�gt �ber eine eigene globale Adresse, die in Messages mitangegeben werden kann. Messages, die das Interface `IAddressedMessage` implementieren, verf�gen �ber eine Zieladresse. Messages, die das Interface `IRequestMessage` implementieren, verf�gen au�erdem �ber eine Quelladresse, die vom BusStop beim Senden automatisch gesetzt wird. 
> In einem Bus bestimmen nicht die Sender einer Message, wer die Message erh�lt. Stattdessen registrieren sich die Empf�nger f�r bestimmte Messages. So kann man sich in einem BusStop f�r Messages registrieren, die die gleiche Zieladresse haben,
wie der BusStop. Es ist aber auch m�glich, jede beliebige andere Message zu erhalten, wenn man will.

`LocalBusEnvironment` definiert bereits einen Default-Busstop:
``` CSharp
IBusStop LocalBusEnvironment.BusStop { get; }
```
F�r das Senden einer Message sind folgende Methoden in einem BusStop vorgesehen:
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
Grunds�tzlich gibt es zwei M�glichkeiten, Messages zu empfangen:
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
Nur auf Messages reagieren, f�r die ein bestimmtes Filterkriterium passt.
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
Eine Message vom Typ IRequestMessage senden und auf den Empfang der zugeh�rigen ResponseMessage warten, die an die Adresse dieses BusStops gesendet wurde.
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
``` CSharp
var timeOutResult = await _env.BusStop.WaitOnMessageToMe<MyResponseMessage>(
	timeoutMs:2000,
	filter:msg => msg.RequestId == request.RequestId && _allowedSources.Contains(msg.Source));
if (!timeOutResult.TimeoutElapsed)
{
	response = timeOutResult.Value;
}
```











