using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Records;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface IEventPubSub
	{
		Task PublishAllAsync();

		Task SubscribeAsync<TEvent>(ISubscriber<WrappedEvent<TEvent>> subscriber) where TEvent : IEvent;
	}
}
