namespace TUtils.Messages.Common.Messages
{
	public interface IResponseMessage : IAddressedMessage
	{
		/// <summary>
		/// Unique in combination with IAddressedMessage.Source
		/// </summary>
		long RequestId { get; set; }
	}
}