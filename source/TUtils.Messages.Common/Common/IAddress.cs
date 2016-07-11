namespace TUtils.Messages.Common.Common
{
	public interface IAddress
	{
		int Hash { get; }
		bool IsEqual(IAddress otherAddress);
	}
}