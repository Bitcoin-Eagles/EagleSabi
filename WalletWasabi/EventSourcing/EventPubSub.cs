using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Interfaces;

namespace WalletWasabi.EventSourcing
{
	public class EventPubSub : IEventPubSub
	{
		#region Dependencies

		protected IEventRepository EventRepository { get; init; }
		protected IPubSub PubSub { get; init; }
		protected IBackgroundTaskQueue BackgroundTaskQueue { get; init; }

		#endregion Dependencies

		public EventPubSub(IEventRepository eventRepository, IPubSub pubSub, IBackgroundTaskQueue backgroundTaskQueue)
		{
			EventRepository = eventRepository;
			PubSub = pubSub;
			BackgroundTaskQueue = backgroundTaskQueue;
		}

		/// <inheritdoc/>
		public async Task PublishAllAsync(CancellationToken cancellationToken)
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
								PubSub.PublishDynamicAsync,
								cancellationToken: cancellationToken)
							.ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
							throw;
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
				}, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async Task SubscribeAsync<TEvent>(ISubscriber<WrappedEvent<TEvent>> subscriber) where TEvent : IEvent
		{
			await PubSub.SubscribeAsync(subscriber).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async Task PublishAllInBackgroundQueueAsync()
		{
			await BackgroundTaskQueue.QueueBackgroundWorkItemAsync(
				async c => await PublishAllAsync(c).ConfigureAwait(false)
			).ConfigureAwait(false);
		}
	}
}
