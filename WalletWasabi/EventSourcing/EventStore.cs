using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	public class EventStore : IEventStore
	{
		public const int OptimisticRetryLimit = 10;

		#region Dependencies

		private IEventRepository EventRepository { get; init; }
		private IAggregateFactory AggregateFactory { get; init; }
		private ICommandProcessorFactory CommandProcessorFactory { get; init; }
		private IEventPubSub? EventPubSub { get; init; }

		#endregion Dependencies

		public EventStore(
			IEventRepository eventRepository,
			IAggregateFactory aggregateFactory,
			ICommandProcessorFactory commandProcessorFactory,
			IEventPubSub? eventPubSub)
		{
			EventRepository = eventRepository;
			AggregateFactory = aggregateFactory;
			CommandProcessorFactory = commandProcessorFactory;
			EventPubSub = eventPubSub;
		}

		/// <inheritdoc />
		public async Task<WrappedResult> ProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			Guard.NotNull(nameof(command), command);
			var tries = OptimisticRetryLimit + 1;
			var optimisticConflict = false;
			do
			{
				tries--;
				optimisticConflict = false;
				try
				{
					return await DoProcessCommandAsync(command, aggregateType, aggregateId).ConfigureAwait(false);
				}
				catch (OptimisticConcurrencyException)
				{
					await Conflicted().ConfigureAwait(false); // No action
					if (tries <= 0)
						throw;
					optimisticConflict = true;
				}
			} while (optimisticConflict && tries > 0);
			throw new AssertionFailedException($"Unexpected code reached in {nameof(ProcessCommandAsync)}");
		}

		/// <inheritdoc />
		public async Task<IAggregate> GetAggregateAsync(string aggregateType, string aggregateId)
		{
			var events = await ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);

			return ApplyEvents(aggregateType, events);
		}

		private async Task<WrappedResult> DoProcessCommandAsync(ICommand command, string aggregateType, string aggregateId)
		{
			var events = await ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
			var aggregate = ApplyEvents(aggregateType, events);
			var lastEvent = events.Count > 0 ? events[^1] : null;
			var sequenceId = lastEvent == null ? 0 : lastEvent.SequenceId;

			bool commandAlreadyProcessed = events.Any(ev => ev.SourceId == command.IdempotenceId);
			if (commandAlreadyProcessed)
			{
				return new WrappedResult(
					sequenceId,
					ImmutableList<WrappedEvent>.Empty,
					aggregate.State,
					IdempotenceIdDuplicate: true);
			}

			if (!CommandProcessorFactory.TryCreate(aggregateType, out var processor))
				throw new AssertionFailedException($"CommandProcessor is missing for aggregate type '{aggregateType}'.");

			Result? result = null;
			List<WrappedEvent>? wrappedEvents = null;
			try
			{
				result = processor.Process(command, aggregate.State);
				if (result.Success)
				{
					wrappedEvents = new();
					foreach (var newEvent in result.Events)
					{
						sequenceId++;
						wrappedEvents.Add(WrappedEvent.CreateDynamic(
							aggregateType, aggregateId, sequenceId, newEvent, command.IdempotenceId));
						aggregate.Apply(newEvent);
					}

					await Prepared().ConfigureAwait(false); // No ation

					await EventRepository.AppendEventsAsync(aggregateType, aggregateId, wrappedEvents)
						.ConfigureAwait(false);

					await Appended().ConfigureAwait(false); // No action
				}
			}
			catch (OptimisticConcurrencyException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new CommandFailedException(
					aggregateType,
					aggregateId,
					sequenceId,
					aggregate.State,
					command,
					ImmutableArray.Create(new Error(ex)),
					null,
					ex);
			}
			if (result?.Success == true)
			{
				if (EventPubSub is not null)
					// TODO: swallow exception and/or publish asynchronously
					await EventPubSub.PublishAllAsync().ConfigureAwait(false);

				await Published().ConfigureAwait(false); // No action

				return new WrappedResult(sequenceId, wrappedEvents!.AsReadOnly(), aggregate.State);
			}
			else if (result?.Success == false)
			{
				throw new CommandFailedException(
					aggregateType,
					aggregateId,
					sequenceId,
					aggregate.State,
					command,
					result.Errors);
			}
			throw new AssertionFailedException($"Unexpected reached in {nameof(EventStore)}.{nameof(DoProcessCommandAsync)}().");
		}

		private IAggregate ApplyEvents(string aggregateType, IReadOnlyList<WrappedEvent> events)
		{
			if (!AggregateFactory.TryCreate(aggregateType, out var aggregate))
				throw new InvalidOperationException($"AggregateFactory is missing for aggregate type '{aggregateType}'.");

			foreach (var wrappedEvent in events)
			{
				aggregate.Apply(wrappedEvent.DomainEvent);
			}

			return aggregate;
		}

		private async Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(string aggregateType, string aggregateId)
		{
			IReadOnlyList<WrappedEvent> events =
				await EventRepository.ListEventsAsync(aggregateType, aggregateId).ConfigureAwait(false);
			return events;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Prepared()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Conflicted()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.

		protected virtual Task Appended()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}

		// Hook for parallel critical section testing.
		protected virtual Task Published()
		{
			// Keep empty. To be overriden in tests.
			return Task.CompletedTask;
		}
	}
}
