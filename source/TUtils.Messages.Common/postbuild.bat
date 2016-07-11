::

:: %1: project dir
:: %2: debug release
:: %3: TUtils.Messages.Common.dll

::**********************************************************
:: copy output to ..\..\lib
::**********************************************************
del "%1..\..\lib\*.*" /f /q /s
copy "%1bin\%2\%3" "%1..\..\lib\%3"
copy "%1..\packages\TUtils.Common\*.*" "%1..\..\lib\*.*"
