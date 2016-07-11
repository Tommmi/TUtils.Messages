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
sind als Klasseninstanzen, die komplett serialisierbar sind. Bei der Gestaltung der .NET Implementation wurde 
viel Wert darauf gelegt, dass man mittels des Schlüsselworts `await` flüssigen Code schreiben kann, was bei 
asynchronen Schnitstellen nicht unbedingt selbstverständlich ist.

`TUtils.Messages` ist Daten-Bus-orientiert: Alle Kommunikationsmodule registrieren sich an einem Bus und können 
Messages senden und empfangen. Die Kommunikation erfolgt asynchron. 

## Wozu eignet sich TUtils.Messages ?
- TUtils.Messages ermöglicht Message-basierte Kommunikation. Darum ist das **Hauptfeature** die Kommunikation 
zwischen zeitgleich laufenden Vorgängen, Tasks und Threads.
- TUtils.Messages ermöglicht die Virtualisierung der Kommunikationsinfrastruktur. Darum lassen sich leicht 
automatisierte Komponenten- oder Systemtests entwickeln, die unabhängig von der Netzwerkinfrastruktur sind.
- *Rein theoretische Betrachtung:* Der virtuelle Bus ist vom Prinzip her nicht Client-Server basiert. 
Darum entfällt (logisch fachlich betrachtet) die Rolle des Servers. Darum kann man (ebenfalls logisch fachlich betrachtet) 
ohne die Programmierung und Wartung eines Servers eine Anwendung betreiben, die über ein weltweites 
Kommunikationsnetzwerk verfügt. Wie kann das sein ? Technisch betrachtet benötigt man zur Nutzung des 
Internets dennoch einen Server, weil die technisch zugrunde liegende Kommunikation Client-Server-basiert ist. 
Dieser muss aber nicht zwingend applikationsspezifisch sein, sondern muss lediglich einer Anwendung einen globalen Bus 
zur Verfügung stellen. Eine Anwendung für ein solches Szenario wäre z.B. eine firmenintern Ad-hoc laufende Teamanwendung, 
ganz ohne Installation eines Servers durch einen Netzwerkadministrator. Da die Technologie keine Rolle spielt, 
kann die Anwendung auch als JavaScript-Programm in einem Browser arbeiten. 

## Programmierung
### Voraussetzungen
### Einbindung
### ..