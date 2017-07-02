import { Promise } from "typescript-dotnet-amd/system/promises/promise";
import IRequestMessage from "../Messages/iRequestMessage";
import IResponseMessage from "../Messages/iResponseMessage";

interface IBusStop {
    // Sends message "request" to request.destination and returns a promise, which succeeds only when 
    // a message of type TResponse has been received with same request id "request.requestId".
    // request.requestId and request.source will be set by this method send() automatically.
    send<TRequest extends IRequestMessage, TResponse extends IResponseMessage>(request: TRequest): Promise<TResponse>;
    // Sends message "request" to request.destination and returns a promise, which will succed only when 
    // a message of type TResponse has been received with same request id "request.requestId" or given timeout 
    // elapsed (see constructor parameter).
    // request.requestId and request.source will be set by this method send() automatically.
    // sendWithTimeoutAndRetry() will retry to send the request up to 10 times within the given timeout.
    // The polling intervall time will be increased be each retry, but won't be smaller than 100 ms.
    sendWithTimeoutAndRetry<TRequest extends IRequestMessage, TResponse extends IResponseMessage>(request: TRequest): Promise<TResponse>;
    // Posts message "message" into bus.
    // If "message" implements interface IAddressedMessage, this method will set property message.Source automatically.
    post(message:any);
}

export default IBusStop;
