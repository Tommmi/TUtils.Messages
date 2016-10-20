::

:: %1: project dir
:: %2: debug release
:: %3: TUtils.Messages.Core.dll

::**********************************************************
:: copy output to ..\..\lib
::**********************************************************
copy "%1bin\%2\%3" "%1..\..\lib\%3"
copy "%1bin\%2\NetSerializer.dll" "%1..\..\lib\NetSerializer.dll"

