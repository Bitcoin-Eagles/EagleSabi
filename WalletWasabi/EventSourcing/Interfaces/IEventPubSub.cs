using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Records;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IEventPubSub
	{
		/// <summary>
		/// Publishes all undelivered events from <see cref="IEventRepository.ListUndeliveredEventsAsync(int?)"/>.
		/// All subscribers receive the event even if some throw an exception. All exceptions are
		/// aggregated into an AggregateException and thrown.
		/// </summary>
		/// <exception cref="AggregateException">any exception from subscribers are aggregated.</exception>
		Task PublishAllAsync();

		/// <summary>
		/// Subscribes <paramref name="subscriber"/> to topic <typeparamref name="TEvent"/>.
		/// </summary>
		/// <typeparam name="TEvent">type of event to be delivered (topic)</typeparam>
		/// <param name="subscriber">subscriber to receive the event</param>
		Task SubscribeAsync<TEvent>(ISubscriber<WrappedEvent<TEvent>> subscriber) where TEvent : IEvent;
	}
}
