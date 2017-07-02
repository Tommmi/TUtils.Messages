import IAddress from "../Common/IAddress";

export interface IAddressedMessage {
    destination: IAddress;
    source: IAddress; 
} 

export default IAddressedMessage;