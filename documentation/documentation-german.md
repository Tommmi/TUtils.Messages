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
sind als Klasseninstanzen, die komplett serialisierbar sind. Bei der Gestaltung der .NET Implementation wurde 
viel Wert darauf gelegt, dass man mittels des Schl�sselworts `await` fl�ssigen Code schreiben kann, was bei 
asynchronen Schnitstellen nicht unbedingt selbstverst�ndlich ist.

`TUtils.Messages` ist Daten-Bus-orientiert: Alle Kommunikationsmodule registrieren sich an einem Bus und k�nnen 
Messages senden und empfangen. Die Kommunikation erfolgt asynchron. 

## Wozu eignet sich TUtils.Messages ?
- TUtils.Messages erm�glicht Message-basierte Kommunikation. Darum ist das **Hauptfeature** die Kommunikation 
zwischen zeitgleich laufenden Vorg�ngen, Tasks und Threads.
- TUtils.Messages erm�glicht die Virtualisierung der Kommunikationsinfrastruktur. Darum lassen sich leicht 
automatisierte Komponenten- oder Systemtests entwickeln, die unabh�ngig von der Netzwerkinfrastruktur sind.
- *Rein theoretische Betrachtung:* Der virtuelle Bus ist vom Prinzip her nicht Client-Server basiert. 
Darum entf�llt (logisch fachlich betrachtet) die Rolle des Servers. Darum kann man (ebenfalls logisch fachlich betrachtet) 
ohne die Programmierung und Wartung eines Servers eine Anwendung betreiben, die �ber ein weltweites 
Kommunikationsnetzwerk verf�gt. Wie kann das sein ? Technisch betrachtet ben�tigt man zur Nutzung des 
Internets dennoch einen Server, weil die technisch zugrunde liegende Kommunikation Client-Server-basiert ist. 
Dieser muss aber nicht zwingend applikationsspezifisch sein, sondern muss lediglich einer Anwendung einen globalen Bus 
zur Verf�gung stellen. Eine Anwendung f�r ein solches Szenario w�re z.B. eine firmenintern Ad-hoc laufende Teamanwendung, 
ganz ohne Installation eines Servers durch einen Netzwerkadministrator. Da die Technologie keine Rolle spielt, 
kann die Anwendung auch als JavaScript-Programm in einem Browser arbeiten. 

## Programmierung
### Voraussetzungen
### Einbindung
### ..