using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.Records
{
	public record Subscriber<TMessage>(Func<TMessage, Task> SubscriberCallback) : Subscriber, ISubscriber<TMessage>
	{
		public async Task Receive(TMessage message)
		{
			await SubscriberCallback.Invoke(message).ConfigureAwait(false);
		}
	}

	public record Subscriber()
	{
		public static Subscriber<TMessage> Create<TMessage>(Func<TMessage, Task> subscriberCallback)
		{
			return new Subscriber<TMessage>(subscriberCallback);
		}

		public static Subscriber<TMessage> Create<TMessage>(Action<TMessage> subscriberCallback)
		{
			return new Subscriber<TMessage>(a => { subscriberCallback.Invoke(a); return Task.CompletedTask; });
		}
	}
}
