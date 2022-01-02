using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;

namespace WalletWasabi.EventSourcing
{
	public class EventPubSub : IEventPubSub
	{
		#region Dependencies

		protected IEventRepository EventRepository { get; init; }
		protected IPubSub PubSub { get; init; }

		#endregion Dependencies

		public EventPubSub(IEventRepository eventRepository, IPubSub pubSub)
		{
			EventRepository = eventRepository;
			PubSub = pubSub;
		}

		/// <inheritdoc/>
		public async Task PublishAllAsync()
		{
			try
			{
				var aggregatesEvents = await EventRepository.ListUndeliveredEventsAsync().ConfigureAwait(false);
				await aggregatesEvents.ForEachAggregatingExceptionsAsync(
					async (aggregateEvents) =>
					{
						if (0 < aggregateEvents.WrappedEvents.Count)
						{
							try
							{
								await aggregateEvents.WrappedEvents.ForEachAggregatingExceptionsAsync(
									PubSub.PublishDynamicAsync
								).ConfigureAwait(false);
							}
							catch
							{
								// TODO: move events into tombstone queue for redelivery
								// with exponential back-off with sprinkle of random delay
								// and then mark those events as delivered in the event store
								// to escape this loop of infinite redelivery attempts
								throw;
							}

							await EventRepository.MarkEventsAsDeliveredCumulativeAsync(
								aggregateEvents.AggregateType,
								aggregateEvents.AggregateId,
								aggregateEvents.WrappedEvents[^1].SequenceId)
								.ConfigureAwait(false);
						}
					}).ConfigureAwait(false);
			}
			catch
			{
				// TODO: Log and swallow exception. This is asynchronous background retriable eventually
				// consistent so the caller doesn't really care if it fails.
				throw;
			}
		}

		/// <inheritdoc/>
		public async Task SubscribeAsync<TEvent>(ISubscriber<WrappedEvent<TEvent>> subscriber) where TEvent : IEvent
		{
			await PubSub.SubscribeAsync(subscriber).ConfigureAwait(false);
		}
	}
}
