export interface IAddress {
    hash: number;
    isEqual(otherAddress: IAddress): boolean;
}

export default IAddress;

