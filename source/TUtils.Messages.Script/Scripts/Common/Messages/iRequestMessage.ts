import IAddressedMessage from "./iAddressedMessage";

export interface IRequestMessage extends IAddressedMessage
{
    // Unique in combination with IAddressedMessage.source
    requestId : number;
}

export default IRequestMessage;