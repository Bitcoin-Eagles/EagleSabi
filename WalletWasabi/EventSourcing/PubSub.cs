using System.Collections.Concurrent;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	public class PubSub : IPubSub
	{
		protected ConcurrentDictionary
			<Type, /* TMessage: type of message */
			ConcurrentBag<Func<object, Task>>> /* subscribers */
			Subscribers
		{ get; init; } = new();

		/// <inheritdoc/>
		public async Task Publish<TMessage>(TMessage message)
		{
			Guard.NotNull(nameof(message), message);
			if (Subscribers.TryGetValue(typeof(TMessage), out var subscribers))
			{
				foreach (var subscriber in subscribers)
				{
					// TODO: Don't stop on exception
#warning aggregateExceptions
					await subscriber.Invoke(message!).ConfigureAwait(false);
				}
			}
		}

		/// <inheritdoc/>
		public Task Subscribe<TMessage>(ISubscriber<TMessage> subscriber)
		{
			var messageTypeSubscribers = Subscribers.GetOrAdd(typeof(TMessage), new ConcurrentBag<Func<object, Task>>());
			messageTypeSubscribers.Add(
				async a => await subscriber.Receive((TMessage)a).ConfigureAwait(false));
			return Task.CompletedTask;
		}
	}
}
