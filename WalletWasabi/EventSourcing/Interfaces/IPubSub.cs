using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IPubSub
	{
		/// <summary>
		/// Publishes <paramref name="message"/> to subscribers. Just subscribers registered
		/// with equal generic type argument <typeparamref name="TMessage"/> will receive
		/// the <paramref name="message"/>. Actual runtime type of <paramref name="message"/>
		/// plays no role in subscriber selection.
		/// </summary>
		/// <typeparam name="TMessage">type of message to be delivered (topic)</typeparam>
		/// <param name="message">message to be delivered to <typeparamref name="TMessage"/> topic</param>
		Task PublishAsync<TMessage>(TMessage message);

		/// <summary>
		/// Subscribes <paramref name="subscriber"/> to topic <typeparamref name="TMessage"/>.
		/// </summary>
		/// <typeparam name="TMessage">type of messages to be delivered (topic)</typeparam>
		/// <param name="subscriber">subscriber to receive the messages</param>
		Task SubscribeAsync<TMessage>(ISubscriber<TMessage> subscriber);
	}
}
