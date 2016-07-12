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

`TUtils.Messages` verfügt derzeit nur über Implementationen für .NET. Eine Implementation für JavaScript ist derzeit in Arbeit.

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
Darum entfällt (fachlich betrachtet) die Rolle des Servers. Zwar können Teilnehmer des Busses Aufgaben eines Services übernehmen, aber die Rolle eines zentralen Servers ist nicht mehr zwingend vorgeschrieben. Darum könnte man ohne die Programmierung und Wartung eines zentralen Applikations-Servers eine Anwendung betreiben, die trotzdem über ein weltweites 
Kommunikationsnetzwerk verfügt. Wie kann das sein ? Technisch betrachtet benötigt man zur Nutzung des Internets als Transportschicht dennoch einen Server, weil die technisch zugrunde liegende Kommunikation Client-Server-basiert ist. Dieser Server muss aber nicht zwingend applikationsspezifisch sein, sondern muss lediglich einer Anwendung einen globalen Bus 
zur Verfügung stellen. Eine Anwendung für ein solches Szenario wäre z.B. eine firmenintern ad-hoc laufende Teamanwendung, 
ganz ohne Installation eines Servers durch einen Netzwerkadministrator. Da die Technologie keine Rolle spielt, 
kann die Anwendung auch als JavaScript-Programm in einem Browser arbeiten. Im Gegensatz zu einer Webanwendung eines Webservice-Anbieters müssen die Daten nicht bei einem Drittanbieter gehostet werden.  
Unabhängig von diesen Überlegungen bleibt aber das Hauptziel von `TUtils.Messages` die Kommunikation zwischen zeitgleich laufenden Vorgängen.

## Programmierung
### Voraussetzungen
- .NET 4.6.1
- (andere Plattformen insbesondere JavaScript sind in Arbeit)

### Einbindung
- aktuelle Version [herunterladen](https://github.com/Tommmi/TUtils.Messages/archive/master.zip).
- entpacken
- Assemblies {repository}\lib\\*.dll in das eigene Projekt einbinden

### Lokalen Bus erzeugen
Die einfachste Möglichkeit, einen lokalen Messagebus zu erzeugen, der Messages innerhalb eines Prozesses transportiert, ist folgende:

``` CSharp
var logger = new Log4NetWriter();
var simpleMessageEnvironment = new LocalBusEnvironment(logger);
```

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

Um einen lokalen Bus zu erzeugen, muss man also die Klasse `MessageBus` instantiieren, deren Konstruktor eine Reihe von DependencyInjection-Parametern verlangt. Instantiieren Sie `MessageBus` manuell, um das Standardverhalten detailliert zu beeinflussen.


### Messages senden
Zwar liefert ein Bus (`IMessageBus`) bereits eine Möglichkeit, Messages zu senden 
``` CSharp
await simpleMessageEnvironment.Bus.SendPort.Enqueue(new MyMessage());
```
dennoch wird empfohlen, nur über einen Busstop auf den Bus zuzugreifen. Ein Busstop ist eine Klasse, die das Interface `IBusStop` implementiert. 
Ein BusStop verfügt über eine eigene globale Adresse, die in Messages mitangegeben werden kann. Messages, die das Interface `IAddressedMessage` implementieren, verfügen über eine Zieladresse. Messages, die das Interface `IRequestMessage` implementieren, verfügen außerdem über eine Quelladresse, die vom BusStop beim Senden automatisch gesetzt wird. **Wichtig**: In einem Bus bestimmen nicht die Sender einer Message, wer die Message erhält. Stattdessen registrieren sich die Empfänger für bestimmte Messages. So kann man sich in einem BusStop für Messages registrieren, die die gleiche Zieladresse haben, wie der BusStop. Es ist aber auch möglich, jede beliebige andere Message zu erhalten, wenn man will.

`LocalBusEnvironment` definiert bereits einen Default-Busstop:
``` CSharp
IBusStop LocalBusEnvironment.BusStop { get; }
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








