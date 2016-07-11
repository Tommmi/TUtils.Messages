using TUtils.Messages.Common.Net;

namespace TUtils.Messages.Common.Queue
{
	public interface IMessageSerializer
	{
		object Deserialize(MessageContent messageContent);
		MessageContent Serialize(object message);
	}
}
