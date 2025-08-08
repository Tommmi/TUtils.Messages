# TUtils.Messages - Agent Instructions

## Build/Test Commands
- **Build**: `msbuild source\TUtils.Messages.sln` or `dotnet build source\TUtils.Messages.sln`
- **Run all tests**: `dotnet test source\TUtils.Messages.sln`
- **Single test**: `dotnet test source\TUtils.Messages.sln --filter TestMethodName`
- **Build configuration**: Debug/Release, targets .NET 8.0

## Known Issues
- **CryptoQueueTest.TestCryptoQueue2**: Disabled due to .NET 8.0 incompatibility with TUtils.Common RSA implementation. The external TUtils.Common library uses RSACryptoServiceProvider which is incompatible with .NET 8.0's RSABCrypt implementation.

## Architecture
- **Messaging framework** based on .NET Task library with message buses, queues, and bridges
- **3 main projects**: TUtils.Messages.Common (interfaces), TUtils.Messages.Core (implementation), TUtils.Messages.Core.Test (MSTest)
- **Key components**: MessageBus, BusStop, Queue adapters, NetClient/NetServer, Bridge, Address system
- **External dependencies**: TUtils.Common, NetSerializer, NSubstitute (tests), Log4Net
- **Communication**: HTTP-based networking, in-process queues, cryptographic queue adapters

## Code Style
- **Namespaces**: TUtils.Messages.{Common|Core|Core.Test}.{SubNamespace}
- **Interfaces**: I prefix (IMessageBus, IQueue), defined in Common project
- **Classes**: PascalCase, implementations in Core project
- **Fields**: _camelCase private fields, PascalCase properties
- **XML documentation**: Required for public APIs
- **Async patterns**: Task-based with async/await, CancellationToken support
- **Error handling**: Exception-based, custom timeout handling
- **Factory pattern**: Extensive use (BusStopFactory, NetClientFactory, etc.)
