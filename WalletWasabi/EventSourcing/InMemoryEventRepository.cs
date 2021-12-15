using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Exceptions;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.EventSourcing.Records;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing
{
	/// <summary>
	/// Thread safe without locks in memory event repository implementation
	/// </summary>
	public class InMemoryEventRepository : IEventRepository
	{
		private const int LIVE_LOCK_LIMIT = 10000;

		private static readonly IReadOnlyList<WrappedEvent> EmptyResult
			= ImmutableList<WrappedEvent>.Empty;

		private static readonly IReadOnlyList<string> EmptyIds
			= ImmutableList<string>.Empty;

		private static readonly IComparer<WrappedEvent> WrappedEventSequenceIdComparer
			= Comparer<WrappedEvent>.Create((a, b) => a.SequenceId.CompareTo(b.SequenceId));

		private ConcurrentDictionary<
			// aggregateType
			string,
			ConcurrentDictionary<
				// aggregateId
				string,
				AggregateEvents>> AggregatesEvents
		{ get; } = new();

		private ConcurrentDictionary<
			// aggregateType
			string,
			AggregateTypeIds> AggregatesIds
		{ get; } = new();

		private ConcurrentDictionary<
			AggregateKey,
			AggregateSequenceIds> UndeliveredSequenceIds
		{ get; } = new();

		/// <inheritdoc/>
		public Task AppendEventsAsync(
			string aggregateType,
			string aggregateId,
			IEnumerable<WrappedEvent> wrappedEvents)
		{
			Guard.NotNullOrEmpty(nameof(aggregateType), aggregateType);
			Guard.NotNullOrEmpty(nameof(aggregateId), aggregateId);
			Guard.NotNull(nameof(wrappedEvents), wrappedEvents);

			var wrappedEventsList = wrappedEvents.ToList().AsReadOnly();
			if (wrappedEventsList.Count == 0)
				return Task.CompletedTask;

			var firstSequenceId = wrappedEventsList[0].SequenceId;
			var lastSequenceId = wrappedEventsList[^1].SequenceId;

			if (firstSequenceId <= 0)
				throw new ArgumentException("First event sequenceId is not natural number.", nameof(wrappedEvents));
			if (lastSequenceId <= 0)
				throw new ArgumentException("Last event sequenceId is not a positive integer.", nameof(wrappedEvents));
			if (lastSequenceId - firstSequenceId + 1 != wrappedEventsList.Count)
				throw new ArgumentException("Event sequence ids are inconsistent.", nameof(wrappedEvents));

			var aggregateEventsBatches = AggregatesEvents.GetOrAdd(aggregateType, _ => new());
			var (tailSequenceId, events) = aggregateEventsBatches.GetOrAdd(
				aggregateId,
				_ => new AggregateEvents(0, ImmutableList<WrappedEvent>.Empty));

			if (tailSequenceId + 1 < firstSequenceId)
				throw new ArgumentException($"Invalid firstSequenceId (gap in sequence ids) expected: '{tailSequenceId + 1}' given: '{firstSequenceId}'.", nameof(wrappedEvents));

			// no action
			Validated();

			var newEvents = events.AddRange(wrappedEventsList);

			// Atomically detect conflict and replace lastSequenceId and lock to ensure strong order in eventsBatches.
			if (!aggregateEventsBatches.TryUpdate(
				key: aggregateId,
				newValue: new AggregateEvents(lastSequenceId, newEvents),
				comparisonValue: new AggregateEvents(firstSequenceId - 1, events)))
			{
				Conflicted(); // no action
				throw new OptimisticConcurrencyException(
					$"Conflict while committing events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			Appended(); // no action

			// If it is a first event for given aggregate.
			if (tailSequenceId == 0)
				// Add index of aggregate id into the dictionary.
				IndexNewAggregateId(aggregateType, aggregateId);

			MarkUndeliveredSequenceIds(aggregateType, aggregateId, tailSequenceId, lastSequenceId);
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<WrappedEvent>> ListEventsAsync(
			string aggregateType,
			string aggregateId,
			long afterSequenceId = 0,
			int? maxCount = null)
		{
			Guard.NotNull(nameof(aggregateType), aggregateType);
			Guard.NotNull(nameof(aggregateId), aggregateId);
			if (AggregatesEvents.TryGetValue(aggregateType, out var aggregateEventsBatches) &&
				aggregateEventsBatches.TryGetValue(aggregateId, out var value))
			{
				var result = value.Events;

				if (afterSequenceId > 0)
				{
					var dummyEvent = new WrappedEvent(afterSequenceId, null!, Guid.Empty);
					var foundIndex = result.BinarySearch(dummyEvent, WrappedEventSequenceIdComparer);
					if (foundIndex < 0)
						// Note: this is because of BinarySearch() documented implementation
						// returns "bitwise complement"
						// see: https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable.immutablelist-1.binarysearch
						// The zero-based index of item in the sorted List, if item is found;
						// otherwise, a negative number that is the bitwise complement
						// of the index of the next element that is larger than item or,
						// if there is no larger element, the bitwise complement of Count.
						foundIndex = ~foundIndex;
					else
						foundIndex++;
					result = result.GetRange(foundIndex, result.Count - foundIndex);
				}
				if (maxCount < result.Count)
					result = result.GetRange(0, maxCount.Value);
				return Task.FromResult((IReadOnlyList<WrappedEvent>)result);
			}
			return Task.FromResult(EmptyResult);
		}

		/// <inheritdoc/>
		public Task MarkEventsAsDeliveredCumulative(string aggregateType, string aggregateId, long deliveredSequenceId)
		{
			var aggregateKey = new AggregateKey(aggregateType, aggregateId);
			var liveLockLimit = LIVE_LOCK_LIMIT;
			var conflict = false;
			var prevDelieveredSequenceId = 0L;
			var tailSequenceId = 0L;
			do
			{
				conflict = false;
				if (liveLockLimit-- <= 0)
					throw new ApplicationException("Live lock detected.");
				var keyExists = false;
				if (UndeliveredSequenceIds.TryGetValue(aggregateKey, out var tuple))
				{
					keyExists = true;

					// If sequenceId is already marked as delivered we are done.
					if (deliveredSequenceId <= tuple.DeliveredSequenceId)
						return Task.CompletedTask;
					prevDelieveredSequenceId = tuple.DeliveredSequenceId;
					tailSequenceId = tuple.TailSequenceId;

					// If all events have been delivered
					if (tailSequenceId == deliveredSequenceId)
					{
						conflict = !UndeliveredSequenceIds.TryRemove(
							KeyValuePair.Create(
								aggregateKey,
								new AggregateSequenceIds(prevDelieveredSequenceId, tailSequenceId)));
					}

					// If some events remain to be delivered
					else if (deliveredSequenceId < tailSequenceId)
					{
						conflict = !UndeliveredSequenceIds.TryUpdate(
							key: aggregateKey,
							newValue: new(deliveredSequenceId, tailSequenceId),
							comparisonValue: new(prevDelieveredSequenceId, tailSequenceId));
					}
				}
				// If MarkUndeliveredSequenceIds() hasn't been called yet
				{
					// If deliveredSequenceId is too high
					if (!AggregatesEvents.TryGetValue(aggregateType, out var aggregates)
						|| !aggregates.TryGetValue(aggregateId, out var aggregateEvents)
						|| aggregateEvents.TailSequenceId < deliveredSequenceId)
					{
						throw new ArgumentException(
							$"{nameof(deliveredSequenceId)} is greater than last appended event's SequenceId",
							nameof(deliveredSequenceId));
					}
					if (keyExists)
					{
						conflict = !UndeliveredSequenceIds.TryUpdate(
							key: aggregateKey,
							newValue: new(deliveredSequenceId, aggregateEvents.TailSequenceId),
							comparisonValue: new(prevDelieveredSequenceId, tailSequenceId));
					}
					else
					{
						conflict = !UndeliveredSequenceIds.TryAdd(
							aggregateKey,
							new(deliveredSequenceId, aggregateEvents.TailSequenceId));
					}
				}
			} while (conflict);
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyList<AggregateUndeliveredEvents>> ListUndeliveredEventsAsync(int? maxCount = null)
		{
			if (maxCount < 1)
				throw new ArgumentOutOfRangeException(nameof(maxCount), $"'{maxCount}' is not positive integer.");
			var result = new List<AggregateUndeliveredEvents>();

			foreach (
				var ((aggregateType, aggregateId), (deliveredSequenceId, tailSequenceId))
				in UndeliveredSequenceIds)
			{
				var events = await ListEventsAsync(aggregateType, aggregateId, deliveredSequenceId, maxCount)
					.ConfigureAwait(false);
				result.Add(new AggregateUndeliveredEvents(aggregateType, aggregateId, events));

				maxCount -= events.Count;
				if (maxCount <= 0)
					break;
			}
			return result.AsReadOnly();
		}

		/// <inheritdoc/>
		public Task<IReadOnlyList<string>> ListAggregateIdsAsync(
			string aggregateType,
			string? afterAggregateId = null,
			int? maxCount = null)
		{
			if (AggregatesIds.TryGetValue(aggregateType, out var aggregateIds))
			{
				var ids = aggregateIds.Ids;
				var foundIndex = 0;
				if (afterAggregateId != null)
				{
					foundIndex = ids.IndexOf(afterAggregateId);
					if (foundIndex < 0)
						foundIndex = ~foundIndex;
					else
						foundIndex++;
				}
				List<string> result = new();
				var afterLastIndex = maxCount.HasValue
					? Math.Min(foundIndex + maxCount.Value, ids.Count)
					: ids.Count;
				for (var i = foundIndex; i < afterLastIndex; i++)
					result.Add(ids[i]);
				return Task.FromResult((IReadOnlyList<string>)result.AsReadOnly());
			}
			return Task.FromResult(EmptyIds);
		}

		private void IndexNewAggregateId(string aggregateType, string aggregateId)
		{
			var tailIndex = 0L;
			ImmutableSortedSet<string> aggregateIds;
			ImmutableSortedSet<string> newAggregateIds;
			var liveLockLimit = LIVE_LOCK_LIMIT;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new ApplicationException("Live lock detected.");
				(tailIndex, aggregateIds) = AggregatesIds.GetOrAdd(aggregateType, _ => new(0, ImmutableSortedSet<string>.Empty));
				newAggregateIds = aggregateIds.Add(aggregateId);
				if (newAggregateIds.Count == aggregateIds.Count)
					throw new ApplicationException($"Aggregate id duplicate detected in '{nameof(InMemoryEventRepository)}.{nameof(IndexNewAggregateId)}'");
			}
			while (!AggregatesIds.TryUpdate(
				key: aggregateType,
				newValue: new AggregateTypeIds(tailIndex + 1, newAggregateIds),
				comparisonValue: new AggregateTypeIds(tailIndex, aggregateIds)));
		}

		private void MarkUndeliveredSequenceIds(
			string aggregateType,
			string aggregateId,
			long prevDeliveredSequenceId,
			long tailSequenceId)
		{
			var aggregateKey = new AggregateKey(aggregateType, aggregateId);
			var liveLockLimit = LIVE_LOCK_LIMIT;
			var deliveredSequenceId = 0L;
			var prevTailSequenceId = 0L;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new ApplicationException("Live lock detected.");
				(deliveredSequenceId, prevTailSequenceId) =
					UndeliveredSequenceIds.GetOrAdd(
						key: aggregateKey,
						value: new(prevDeliveredSequenceId, tailSequenceId));

				// If key was newly added to the dictionary or
				// there is already greater TailSequenceId we are done.
				if (tailSequenceId <= prevTailSequenceId)
					return;
			}
			while (!UndeliveredSequenceIds.TryUpdate(
					key: aggregateKey,
					newValue: new(deliveredSequenceId, tailSequenceId),
					comparisonValue: new(deliveredSequenceId, prevTailSequenceId)));
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Validated()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Conflicted()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Appended()
		{
			// Keep empty. To be overriden in tests.
		}
	}
}
