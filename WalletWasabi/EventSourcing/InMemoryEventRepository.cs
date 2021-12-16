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

			var aggregatesEvents = AggregatesEvents.GetOrAdd(aggregateType, _ => new());
			var prevEvents = aggregatesEvents.GetOrAdd(
				aggregateId,
				_ => new(0, ImmutableList<WrappedEvent>.Empty));

			if (prevEvents.TailSequenceId + 1 < firstSequenceId)
				throw new ArgumentException($"Invalid firstSequenceId (gap in sequence ids) expected: '{prevEvents.TailSequenceId + 1}' given: '{firstSequenceId}'.", nameof(wrappedEvents));

			Append_Validated(); // no action

			var newEvents = prevEvents.Events.AddRange(wrappedEventsList);
			var newValue = new AggregateEvents(lastSequenceId, newEvents);
			var comparisonValue = prevEvents with { TailSequenceId = firstSequenceId - 1 };

			MarkUndeliveredSequenceIds(aggregateType, aggregateId, firstSequenceId, lastSequenceId, prevEvents);

			Append_MarkedUndelivered(); // no action

			// Atomically detect conflict and replace lastSequenceId.
			if (!aggregatesEvents.TryUpdate(aggregateId, newValue, comparisonValue))
			{
				Append_Conflicted(); // no action
				throw new OptimisticConcurrencyException(
					$"Conflict while committing events. Retry command. aggregate: '{aggregateType}' id: '{aggregateId}'");
			}
			Append_Appended(); // no action

			// If it is a first event for given aggregate.
			if (prevEvents.TailSequenceId == 0)
				// Add index of aggregate id into the dictionary.
				IndexNewAggregateId(aggregateType, aggregateId);

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
			AggregateEvents? aggregateEvents;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new ApplicationException("Live lock detected.");

				// If deliveredSequenceId is too high
				if (!AggregatesEvents.TryGetValue(aggregateType, out var aggregates)
					|| !aggregates.TryGetValue(aggregateId, out aggregateEvents)
					|| aggregateEvents.TailSequenceId < deliveredSequenceId)
				{
					throw new ArgumentException(
						$"{nameof(deliveredSequenceId)} is greater than last appended event's SequenceId (than '{nameof(AggregateEvents.TailSequenceId)}')",
						nameof(deliveredSequenceId));
				}
			}
			while (!TryDoMarkEventsAsDeliveredComulative(aggregateKey, aggregateEvents, deliveredSequenceId));

			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyList<AggregateUndeliveredEvents>> ListUndeliveredEventsAsync(int? maxCount = null)
		{
			if (maxCount < 1)
				throw new ArgumentOutOfRangeException(nameof(maxCount), $"'{maxCount}' is not positive integer.");
			var result = new List<AggregateUndeliveredEvents>();

			foreach (var (key, sequenceIds) in UndeliveredSequenceIds)
			{
				var events = await ListEventsAsync(key.AggregateType, key.AggregateId, sequenceIds.DeliveredSequenceId, maxCount)
					.ConfigureAwait(false);

				if (0 < events.Count)
				{
					result.Add(new AggregateUndeliveredEvents(key.AggregateType, key.AggregateId, events));
				}
				else if (AggregatesEvents.TryGetValue(key.AggregateType, out var aggregatesEvents)
					&& aggregatesEvents.TryGetValue(key.AggregateId, out var aggregateEvents))
				{
					TryFixUndeliveredSequenceIdsAfterAppendConflict(
						key,
						aggregateEvents,
						sequenceIds.DeliveredSequenceId,
						sequenceIds);
				}

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
			long transactionFirstSequenceId,
			long transactionLastSequenceId,
			AggregateEvents aggregateEvents)
		{
			var aggregateKey = new AggregateKey(aggregateType, aggregateId);
			var liveLockLimit = LIVE_LOCK_LIMIT;
			AggregateSequenceIds? previous;
			do
			{
				if (liveLockLimit-- <= 0)
					throw new ApplicationException("Live lock detected.");
				var newValue = new AggregateSequenceIds(
					transactionFirstSequenceId - 1,
					transactionFirstSequenceId,
					transactionLastSequenceId);
				previous = UndeliveredSequenceIds.GetOrAdd(aggregateKey, newValue);

				// If key was newly added to the dictionary we are done.
				if (previous == newValue)
				{
					return;
				}
				// If there is already greater TransactionLastSequenceId try
				// to fix after possible conflict and retry.
				else if (transactionLastSequenceId < previous.TransactionLastSequenceId)
				{
					if (TryFixUndeliveredSequenceIdsAfterAppendConflict(
						new(aggregateType, aggregateId),
						aggregateEvents,
						previous.DeliveredSequenceId,
						previous))
					{
						continue;
					}
					else
					{
						return;
					}
				}
			}
			while (!UndeliveredSequenceIds.TryUpdate(
				key: aggregateKey,
				newValue: new(previous.DeliveredSequenceId, transactionFirstSequenceId, transactionLastSequenceId),
				comparisonValue: previous));
		}

		private bool TryDoMarkEventsAsDeliveredComulative(
			AggregateKey aggregateKey,
			AggregateEvents aggregateEvents,
			long deliveredSequenceId)
		{
			if (UndeliveredSequenceIds.TryGetValue(aggregateKey, out var previous))
			{
				if (previous.TransactionLastSequenceId < aggregateEvents.TailSequenceId)
					throw new ApplicationException($"'{nameof(UndeliveredSequenceIds)}' is inconsistnet with '{nameof(AggregatesEvents)}'. '{nameof(AggregateSequenceIds.TransactionLastSequenceId)}' is smaller than '{nameof(AggregateEvents.TailSequenceId)}'. (aggregateKey: '{aggregateKey}')");

				if (TryFixUndeliveredSequenceIdsAfterAppendConflict(aggregateKey, aggregateEvents, deliveredSequenceId, previous))
				{
					// Conflict has been fixed or another conflict detected we need to retry hence return false;
					return false;
				}
				else
				{
					// If sequenceId is already marked as delivered we are done.
					if (deliveredSequenceId <= previous.DeliveredSequenceId)
						return true;

					// If all events have been delivered
					if (previous.TransactionLastSequenceId == deliveredSequenceId)
					{
						return UndeliveredSequenceIds.TryRemove(KeyValuePair.Create(aggregateKey, previous));
					}
					// If some events remain to be delivered
					else if (deliveredSequenceId < previous.TransactionLastSequenceId)
					{
						var newValue = previous with { DeliveredSequenceId = deliveredSequenceId };
						return UndeliveredSequenceIds.TryUpdate(aggregateKey, newValue, previous);
					}
					else
					{
						// At this point one of the previous exceptions should have been thrown.
						throw new ApplicationException($"Unexpected code reached in '{nameof(TryDoMarkEventsAsDeliveredComulative)}'.");
					}
				}
			}
			else
			{
				// 'deliveredSequenceId' has been already marked so we are done.
				return true;
			}
		}

		private bool TryFixUndeliveredSequenceIdsAfterAppendConflict(
			AggregateKey aggregateKey,
			AggregateEvents aggregateEvents,
			long deliveredSequenceId,
			AggregateSequenceIds previous)
		{
			// If there has been conflict previously in AppendEventsAsync() and
			// previous.TransactionLastSequenceId is too high
			if (previous.TransactionFirstSequenceId <= aggregateEvents.TailSequenceId
				&& aggregateEvents.TailSequenceId < previous.TransactionLastSequenceId)
			{
				if (deliveredSequenceId == aggregateEvents.TailSequenceId)
				{
					UndeliveredSequenceIds.TryRemove(KeyValuePair.Create(aggregateKey, previous));
				}
				else
				{
					var newValue = previous with { TransactionLastSequenceId = aggregateEvents.TailSequenceId };
					UndeliveredSequenceIds.TryUpdate(aggregateKey, newValue, previous);
				}
				// Regardless whether it has been fixed or there is another conflict
				// in UndeliveredSequenceIds we need to retry so return true eitherway.
				return true;
			}
			else
			{
				return false;
			}
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Append_Validated()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Append_MarkedUndelivered()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Append_Conflicted()
		{
			// Keep empty. To be overriden in tests.
		}

		// Hook for parallel critical section testing in DEBUG build only.
		[Conditional("DEBUG")]
		protected virtual void Append_Appended()
		{
			// Keep empty. To be overriden in tests.
		}
	}
}
