// ReSharper disable ConvertPropertyToExpressionBody
// ReSharper disable ConvertToAutoProperty
namespace TUtils.Messages.Common.Common
{
	public class TimeoutResult<T>
	{
		public T Value { get; private set; }

		public bool TimeoutElapsed { get; private set; }

		public TimeoutResult(T value, bool timeoutElapsed)
		{
			Value = value;
			TimeoutElapsed = timeoutElapsed;
		}
	}
}