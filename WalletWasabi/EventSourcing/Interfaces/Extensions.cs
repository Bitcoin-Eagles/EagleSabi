using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public static class Extensions
	{
		public static async Task PublishDynamicAsync(this IPubSub pubSub, dynamic message)
		{
			await pubSub.PublishAsync(message).ConfigureAwait(false);
		}
	}
}
