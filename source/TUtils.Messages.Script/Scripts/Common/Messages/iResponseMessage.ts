import IAddressedMessage from "./iAddressedMessage";

export interface IResponseMessage extends IAddressedMessage
{
    // Unique in combination with IAddressedMessagesSource
    requestId: number;
}

export default IResponseMessage;