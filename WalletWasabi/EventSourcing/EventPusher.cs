using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing
{
	public class EventPusher : IEventPusher
	{
		#region Dependencies

		protected IEventRepository EventRepository { get; init; }
		protected IPubSub PubSub { get; init; }

		#endregion Dependencies

		public EventPusher(IEventRepository eventRepository, IPubSub pubSub)
		{
			EventRepository = eventRepository;
			PubSub = pubSub;
		}

		public async Task PushAsync()
		{
			try
			{
				var aggregatesEvents = await EventRepository.ListUndeliveredEventsAsync().ConfigureAwait(false);
				await aggregatesEvents.ForEachAggregateExceptionsAsync(
					async (aggregateEvents) =>
					{
						if (0 < aggregateEvents.WrappedEvents.Count)
						{
							try
							{
								await aggregateEvents.WrappedEvents.ForEachAggregateExceptionsAsync(
									async (@event) =>
										await PubSub.PublishDynamicAsync(@event).ConfigureAwait(false)
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
	}
}
